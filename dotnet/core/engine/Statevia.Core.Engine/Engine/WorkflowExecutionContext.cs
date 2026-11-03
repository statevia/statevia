using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using System.Globalization;
using System.Text.Json;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// 1 実行インスタンスの実行時データ（Execution Context）を保持する。
/// </summary>
/// <remarks>
/// <para>
/// 定義 YAML の <c>input</c> / <c>output</c> パス式（SimpleJsonPath）の評価根となる。
/// トップレベルは <c>input</c> / <c>output</c> / <c>states</c> / <c>vars</c> / <c>sys</c> 固定。
/// </para>
/// <para>
/// 型名は <see cref="System.Threading.ExecutionContext"/> との衝突を避けるため
/// <c>WorkflowExecutionContext</c> とする（概念名は Execution Context）。
/// </para>
/// <para>
/// <c>vars</c> は <see cref="SetVar"/> で読み書き可能。<c>sys</c> は評価時スナップショットの読み取り専用ランタイム情報。
/// </para>
/// <para>
/// この実行で書いた <c>vars</c> のパスと完了した状態名を記録する。Hosted Fork の枝では、Join で親へ戻す範囲になる
/// （<see cref="ExportBranchWrites"/>）。Fork 到達時に親から引き継いだ値は記録しない。
/// </para>
/// </remarks>
public sealed class WorkflowExecutionContext
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyObject =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();
    private readonly Dictionary<string, object?> _states = new(StringComparer.OrdinalIgnoreCase);
    private object? _workflowOutput = EmptyObject;
    private object? _vars = EmptyObject;
    private readonly string _sysExecutionId;
    private readonly string _definitionName;

    // この実行で書いた vars のパス（$.vars 以下のプロパティ名の並び）。祖先のパスがあれば子孫は持たない。
    private readonly List<string[]> _writtenVarPaths = [];

    // この実行で完了した（または Join で取り込んだ）状態名。
    private readonly HashSet<string> _writtenStates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>ワークフロー開始時に設定した input（不変）。</summary>
    public object? Input { get; }

    /// <summary>
    /// 開始 input と実行メタデータで Context を生成する。
    /// </summary>
    /// <param name="input"><see cref="Abstractions.IExecutionEngine.Start"/> に渡された開始 input。</param>
    /// <param name="executionId">実行インスタンス ID（<c>$.sys.execution.id</c>）。</param>
    /// <param name="definitionName">コンパイル済み定義名（<c>$.sys.definition.name</c>）。</param>
    /// <returns>初期化済み Context。<c>states</c> / <c>vars</c> は空。</returns>
    public static WorkflowExecutionContext Create(
        object? input,
        string executionId = "",
        string definitionName = "") =>
        new(input, executionId, definitionName);

    /// <summary>
    /// Hosted Fork の枝（子 execution）として Context を生成する。
    /// </summary>
    /// <remarks>
    /// <para><c>vars</c> / <c>states</c> は Fork 到達時点の親の写しで初期化し、書いた値には数えない。
    /// <c>$.sys.execution.id</c> は親の論理実行 ID にする。</para>
    /// </remarks>
    /// <param name="branch">枝の開始指定。</param>
    /// <param name="definitionName">コンパイル済み定義名（<c>$.sys.definition.name</c>）。</param>
    /// <returns>初期化済み Context。</returns>
    public static WorkflowExecutionContext CreateForkBranch(ForkBranchStart branch, string definitionName)
    {
        ArgumentNullException.ThrowIfNull(branch);
        var parent = branch.ParentContext;
        var ctx = new WorkflowExecutionContext(branch.WorkflowInput, parent.LogicalExecutionId, definitionName);
        lock (ctx._lock)
        {
            ctx._vars = NormalizeTree(parent.Vars) ?? EmptyObject;
            if (NormalizeTree(parent.States) is IReadOnlyDictionary<string, object?> states)
            {
                foreach (var (name, entry) in states)
                {
                    ctx._states[name] = entry;
                }
            }
        }

        return ctx;
    }

    /// <summary>チェックポイント断面から Context を復元する。</summary>
    /// <remarks>
    /// 書いたパスの記録が無い checkpoint（記録を始める前に保存したもの）は、<c>vars</c> の全トップレベルキーと全状態を書いたものとして扱う。
    /// その時点の枝は親の値を引き継いでいないため、値を落とさない安全側になる。
    /// </remarks>
    /// <param name="data">Context 断面。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="definitionName">定義名。</param>
    /// <returns>復元済み Context。</returns>
    public static WorkflowExecutionContext CreateFromCheckpoint(
        CheckpointContextData data,
        string executionId,
        string definitionName)
    {
        ArgumentNullException.ThrowIfNull(data);
        var ctx = new WorkflowExecutionContext(
            CheckpointJson.FromElement(data.Input),
            data.SysExecutionId ?? executionId,
            definitionName);
        lock (ctx._lock)
        {
            ctx._workflowOutput = CheckpointJson.FromElement(data.WorkflowOutput) ?? EmptyObject;
            ctx._vars = CheckpointJson.FromElement(data.Vars) ?? EmptyObject;
            foreach (var (name, entry) in data.States)
            {
                ctx._states[name] = CheckpointJson.FromElement(entry);
            }

            var writtenVarPaths = data.WrittenVarPaths
                ?? (ctx._vars as IReadOnlyDictionary<string, object?> ?? EmptyObject).Keys
                    .Select(key => (IReadOnlyList<string>)[key])
                    .ToList();
            ctx._writtenVarPaths.AddRange(writtenVarPaths.Select(path => path.ToArray()));
            ctx._writtenStates.UnionWith(data.WrittenStates ?? data.States.Keys);
        }

        return ctx;
    }

    /// <summary>チェックポイント用に Context 断面をエクスポートする。</summary>
    public CheckpointContextData ExportCheckpoint()
    {
        lock (_lock)
        {
            return new CheckpointContextData
            {
                Input = CheckpointJson.ToElement(Input),
                WorkflowOutput = CheckpointJson.ToElement(_workflowOutput),
                Vars = CheckpointJson.ToElement(_vars),
                States = _states.ToDictionary(
                    kv => kv.Key,
                    kv => CheckpointJson.ToElement(kv.Value),
                    StringComparer.OrdinalIgnoreCase),
                SysExecutionId = _sysExecutionId,
                WrittenVarPaths = _writtenVarPaths.Select(path => (IReadOnlyList<string>)path).ToList(),
                WrittenStates = _writtenStates.ToList()
            };
        }
    }

    /// <summary>
    /// Fork 到達時に、各枝が引き継ぐ <c>vars</c> / <c>states</c> の写しと論理実行 ID を取り出す。
    /// </summary>
    /// <returns>以後の書き込みの影響を受けない写し。</returns>
    public ForkParentContext ExportForkParentContext()
    {
        lock (_lock)
        {
            var states = _states.ToDictionary(
                kv => kv.Key,
                kv => CloneForIsolation(kv.Value),
                StringComparer.OrdinalIgnoreCase);
            return new ForkParentContext(SnapshotObject(_vars), states, _sysExecutionId);
        }
    }

    /// <summary>
    /// チェックポイント断面から、この実行が書いた値だけを Join の断片として取り出す。
    /// </summary>
    /// <remarks>
    /// <para>Hosted Fork の枝の終端で使う。引き継いだだけの値は含めない。値は <see cref="JsonElement"/> にする。</para>
    /// <para>記録したパスの値が無いときは、そのパスを含めない。</para>
    /// </remarks>
    /// <param name="data">枝の終端時点の Context 断面。</param>
    /// <returns>書いた <c>states</c> と <c>vars</c> のパスと値。</returns>
    public static PhysicalJoinContextFragment ExportBranchWrites(CheckpointContextData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var ctx = CreateFromCheckpoint(data, executionId: string.Empty, definitionName: string.Empty);
        lock (ctx._lock)
        {
            var states = ctx._writtenStates
                .Where(ctx._states.ContainsKey)
                .ToDictionary(
                    name => name,
                    name => (object?)CheckpointJson.ToElement(ctx._states[name]),
                    StringComparer.OrdinalIgnoreCase);
            var varWrites = ctx._writtenVarPaths
                .Select(path => (Path: path, Found: TryGetVarValue(ctx._vars, path, out var value), Value: value))
                .Where(entry => entry.Found)
                .Select(entry => new ContextVarWrite(entry.Path, CheckpointJson.ToElement(entry.Value)))
                .ToList();
            return new PhysicalJoinContextFragment(states, varWrites);
        }
    }

    private WorkflowExecutionContext(object? input, string sysExecutionId, string definitionName)
    {
        Input = input;
        _sysExecutionId = sysExecutionId ?? string.Empty;
        _definitionName = definitionName ?? string.Empty;
    }

    /// <summary>指定状態が完了済み（output 記録済み）か。</summary>
    /// <param name="stateName">定義上の状態名。</param>
    /// <returns>完了済みのとき true。</returns>
    public bool HasStateOutput(string stateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        lock (_lock)
        {
            return _states.ContainsKey(stateName);
        }
    }

    /// <summary>
    /// 完了済み State エントリを一括投影する（既存キーは上書き＝後勝ち）。
    /// </summary>
    /// <param name="states">stateName → `{ output: … }` 形式のエントリ。</param>
    public void MergeStateEntries(IReadOnlyDictionary<string, object?> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        lock (_lock)
        {
            foreach (var (stateName, entry) in states)
            {
                if (string.IsNullOrWhiteSpace(stateName))
                    continue;
                _states[stateName] = CloneForIsolation(entry);
                _writtenStates.Add(stateName);
            }
        }
    }

    /// <summary>
    /// Join で枝が書いた <c>vars</c> のパスを、渡された順に代入する（同じパス・重なるパスは後勝ち）。
    /// </summary>
    /// <remarks>
    /// 代入はパスの部分木の置き換え。代入したパスはこの実行が書いたパスとして記録する（ネストした Fork で、さらに外側の Join へ戻すため）。
    /// </remarks>
    /// <param name="writes">書き込みの並び。</param>
    public void ApplyVarWrites(IReadOnlyList<ContextVarWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);
        lock (_lock)
        {
            foreach (var write in writes)
            {
                SetVarCore(write.Path, NormalizeTree(write.Value));
            }
        }
    }

    /// <summary>
    /// 状態完了時の output を <c>states.&lt;StateName&gt;.output</c> として記録する。
    /// </summary>
    /// <remarks>
    /// 辞書ツリーは隔離コピーする。同一参照を <see cref="SetVar"/> した場合でも、
    /// 後続のネスト代入で履歴が壊れない。
    /// </remarks>
    /// <param name="stateName">定義上の状態名。</param>
    /// <param name="output">状態の成功 output（失敗時は呼ばない想定）。</param>
    public void SetStateOutput(string stateName, object? output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        lock (_lock)
        {
            // vars への後続ネスト代入や呼び出し元の破壊的更新から履歴を守るため、辞書ツリーは隔離コピーする。
            _states[stateName] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["output"] = CloneForIsolation(output)
            };
            _writtenStates.Add(stateName);
        }
    }

    /// <summary>
    /// <c>$.vars</c> 配下へ値を代入する。中間オブジェクトは自動生成し、既存キーは上書きする。
    /// </summary>
    /// <remarks>
    /// 代入値の辞書ツリーは隔離コピーする。<see cref="SetStateOutput"/> と参照を共有しない。
    /// </remarks>
    /// <param name="varsPath"><c>$.vars</c> または <c>$.vars.&lt;seg&gt;…</c>（SimpleJsonPath）。</param>
    /// <param name="value">代入する値。</param>
    /// <exception cref="ArgumentException"><paramref name="varsPath"/> が <c>$.vars</c> 配下でない、または不正なとき。</exception>
    public void SetVar(string varsPath, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(varsPath);
        // 「$.vars 配下か」の判定は Level1 検証（output）と同一の実装を共有する。
        if (!ExecutionContextPathResolver.IsVarsWritePath(varsPath)
            || !SimpleJsonPath.TryGetSegments(varsPath, out var segments))
        {
            throw new ArgumentException(
                $"vars path must be under $.{ExecutionContextKeys.Vars}: {varsPath}",
                nameof(varsPath));
        }

        if (segments.Skip(1).Any(segment => segment.Name is null))
            throw new ArgumentException("vars write path cannot contain array index segments.", nameof(varsPath));

        var path = segments.Skip(1).Select(segment => segment.Name!).ToArray();
        lock (_lock)
        {
            // states 記録や呼び出し元オブジェクトと参照を共有しないよう、代入値は隔離コピーする。
            SetVarCore(path, CloneForIsolation(value));
        }
    }

    /// <summary>パス（<c>$.vars</c> 以下のプロパティ名の並び）へ代入し、書いたパスとして記録する。ロック内で呼ぶ。</summary>
    private void SetVarCore(IReadOnlyList<string> path, object? isolated)
    {
        if (path.Count == 0)
        {
            _vars = isolated ?? EmptyObject;
        }
        else
        {
            SetNestedValue(EnsureVarsDictionary(), path, isolated);
        }

        RecordWrittenVarPath(path);
    }

    /// <summary>
    /// 書いたパスを記録する。祖先（同じパスを含む）が記録済みなら何もしない。祖先を書いたら記録済みの子孫を除く。ロック内で呼ぶ。
    /// </summary>
    private void RecordWrittenVarPath(IReadOnlyList<string> path)
    {
        if (_writtenVarPaths.Any(written => IsPathPrefix(written, path)))
            return;

        _writtenVarPaths.RemoveAll(written => IsPathPrefix(path, written));
        _writtenVarPaths.Add(path.ToArray());
    }

    /// <summary><paramref name="prefix"/> が <paramref name="path"/> 自身か祖先か（大文字小文字は区別しない）。</summary>
    private static bool IsPathPrefix(IReadOnlyList<string> prefix, IReadOnlyList<string> path) =>
        prefix.Count <= path.Count
        && prefix.Select((name, index) => string.Equals(name, path[index], StringComparison.OrdinalIgnoreCase)).All(equal => equal);

    /// <summary><c>vars</c> ツリーからパスの値を取り出す。途中が辞書でない・キーが無いときは false。</summary>
    private static bool TryGetVarValue(object? vars, IReadOnlyList<string> path, out object? value)
    {
        value = vars;
        foreach (var name in path)
        {
            if (value is not IReadOnlyDictionary<string, object?> dict || !dict.TryGetValue(name, out value))
            {
                value = null;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// ワークフロー終端時の最終 output を設定する（通常 1 回）。
    /// </summary>
    /// <param name="output">終端 State の output。</param>
    public void SetWorkflowOutput(object? output)
    {
        lock (_lock)
        {
            _workflowOutput = output ?? EmptyObject;
        }
    }

    /// <summary>
    /// SimpleJsonPath 評価用に Context 全体を辞書としてスナップショットする。
    /// </summary>
    /// <returns>
    /// <c>input</c> / <c>output</c> / <c>states</c> / <c>vars</c> / <c>sys</c> を持つ読み取り用ツリー。
    /// </returns>
    public IReadOnlyDictionary<string, object?> ToPathRoot()
    {
        lock (_lock)
        {
            var statesCopy = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, entry) in _states)
            {
                statesCopy[name] = entry;
            }

            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [ExecutionContextKeys.Input] = Input,
                [ExecutionContextKeys.Output] = _workflowOutput,
                [ExecutionContextKeys.States] = statesCopy,
                [ExecutionContextKeys.Vars] = SnapshotObject(_vars),
                [ExecutionContextKeys.Sys] = BuildSysSnapshot()
            };
        }
    }

    private Dictionary<string, object?> EnsureVarsDictionary()
    {
        // EmptyObject は共有センチネルのため書き換えない（並列テスト間の汚染を防ぐ）。
        if (_vars is Dictionary<string, object?> existing
            && !ReferenceEquals(existing, EmptyObject))
        {
            return existing;
        }

        var created = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (_vars is IReadOnlyDictionary<string, object?> readOnly
            && !ReferenceEquals(readOnly, EmptyObject))
        {
            foreach (var (key, entry) in readOnly)
            {
                created[key] = entry;
            }
        }

        _vars = created;
        return created;
    }

    private static void SetNestedValue(
        Dictionary<string, object?> root,
        IReadOnlyList<string> path,
        object? value)
    {
        // 直前に返した ToPathRoot スナップショットは入れ子辞書の参照を共有するため、
        // 書き込み経路の中間辞書を都度クローンして既存参照を破壊しない（copy-on-write）。
        // これにより fork 並列時に「別分岐がスナップショットを読みつつ vars を書き込む」競合を防ぐ。
        var current = root;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var key = path[i];
            var nextDict = current.TryGetValue(key, out var next)
                    && next is IReadOnlyDictionary<string, object?> existingDict
                ? new Dictionary<string, object?>(existingDict, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            current[key] = nextDict;
            current = nextDict;
        }

        current[path[^1]] = value;
    }

    private static object? SnapshotObject(object? value) =>
        CloneForIsolation(value) ?? EmptyObject;

    /// <summary>
    /// 引き継ぎ値や Join の値を Context に入れる形にする。<see cref="JsonElement"/> は辞書・リストのツリーに戻し、
    /// 辞書ツリーは隔離コピーする（後続のネスト代入で中間辞書を辿れるようにする）。
    /// </summary>
    private static object? NormalizeTree(object? value) =>
        value switch
        {
            JsonElement element => CheckpointJson.FromElement(element),
            _ => CloneForIsolation(value)
        };

    /// <summary>
    /// 辞書ツリーを再帰コピーし、スカラー等はそのまま返す。Context 内の states / vars 間の参照共有を防ぐ。
    /// </summary>
    private static object? CloneForIsolation(object? value)
    {
        if (value is null || ReferenceEquals(value, EmptyObject))
        {
            return value;
        }

        if (value is Dictionary<string, object?> dict)
        {
            var clone = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, entry) in dict)
            {
                clone[key] = CloneForIsolation(entry);
            }

            return clone;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnly)
        {
            var clone = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, entry) in readOnly)
            {
                clone[key] = CloneForIsolation(entry);
            }

            return clone;
        }

        return value;
    }

    private Dictionary<string, object?> BuildSysSnapshot()
    {
        var now = DateTimeOffset.Now;
        var utcNow = DateTimeOffset.UtcNow;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["now"] = now.ToString("O", CultureInfo.InvariantCulture),
            ["today"] = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["utcNow"] = utcNow.ToString("O", CultureInfo.InvariantCulture),
            ["execution"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = _sysExecutionId
            },
            ["definition"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = _definitionName
            }
        };
    }
}
