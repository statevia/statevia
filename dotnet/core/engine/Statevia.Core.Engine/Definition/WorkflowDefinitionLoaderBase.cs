using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition.Validation;
using System.Collections;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// ワークフロー定義ローダーの共通基盤。
/// デシリアライズと Template Method の骨格を提供する。
/// </summary>
public abstract class WorkflowDefinitionLoaderBase : IDefinitionLoader
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// デシリアライザを構築する。
    /// </summary>
    /// <param name="useScalarPreservingNodeTypeResolver">
    /// <see langword="true"/> のとき、スカラー型を保持するノード型リゾルバを有効にする。
    /// </param>
    protected WorkflowDefinitionLoaderBase(bool useScalarPreservingNodeTypeResolver = false)
    {
        var builder = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties();

        if (useScalarPreservingNodeTypeResolver)
        {
            builder = builder.WithNodeTypeResolver(new ScalarPreservingNodeTypeResolver());
        }

        _deserializer = builder.Build();
    }

    /// <inheritdoc />
    public WorkflowDefinition Load(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var raw = _deserializer.Deserialize<object>(new StringReader(content))
            ?? throw new InvalidOperationException("Invalid YAML/JSON");
        var root = ToStringDict(raw);
        return BuildDefinition(root);
    }

    /// <summary>デシリアライズ済みルートオブジェクトから定義を構築する。</summary>
    protected abstract WorkflowDefinition BuildDefinition(Dictionary<string, object?> root);

    /// <summary>
    /// 厳格仕様の input マッピングを解釈する。
    /// ${...} は拒否し、$. パスは単純 JSONPath 制約で検証する。
    /// </summary>
    protected static StateInputDefinition? ParseStrictInputMapping(object? inputVal, string? ownerLabel = null)
    {
        if (inputVal == null)
        {
            return null;
        }

        if (inputVal is string s)
        {
            return ParseStrictInputFromString(s, ownerLabel);
        }

        var map = ToStringDict(inputVal, StringComparer.OrdinalIgnoreCase);
        var singlePath = TryParseStrictInputSinglePath(map, ownerLabel);
        if (singlePath is not null)
        {
            return singlePath;
        }

        return ParseStrictInputFromMap(map, ownerLabel);
    }

    /// <summary>スカラー文字列の input を <see cref="StateInputDefinition"/> に解釈する。</summary>
    /// <param name="s">YAML/JSON 由来の文字列（パス式またはリテラル）。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>単一 <c>path</c> または <c>values.value</c> リテラルとしての定義。</returns>
    private static StateInputDefinition ParseStrictInputFromString(string s, string? ownerLabel)
    {
        RejectTemplate(ownerLabel, s);
        if (IsPathExpression(s))
        {
            EnsureReadPath(s, ownerLabel, $"invalid input path: '{s}'");
            return new StateInputDefinition { Path = s };
        }

        return new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = new StateInputValueDefinition { Literal = s }
            }
        };
    }

    /// <summary><c>{ path: "$...." }</c> 形式の単一キー input を解釈する。</summary>
    /// <param name="map">input の辞書。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>単一 path として解釈できたときの定義。該当しないとき null。</returns>
    private static StateInputDefinition? TryParseStrictInputSinglePath(
        Dictionary<string, object?> map,
        string? ownerLabel)
    {
        if (map.Count != 1
            || !map.TryGetValue("path", out var pathVal)
            || pathVal is not string onlyPath)
        {
            return null;
        }

        RejectTemplate(ownerLabel, onlyPath);
        if (!IsPathExpression(onlyPath))
        {
            return null;
        }

        EnsureReadPath(onlyPath, ownerLabel, $"invalid input.path: '{onlyPath}'");
        return new StateInputDefinition { Path = onlyPath };
    }

    /// <summary>複数キーの input マップを <c>values</c> 付き <see cref="StateInputDefinition"/> に解釈する。</summary>
    /// <remarks>
    /// <para>
    /// 空でないネスト map は <c>親.子</c> のドットキーへ展開する（<see cref="FlattenStrictInputEntry"/>）。
    /// 例: <c>body: { orderId: $.x, currency: JPY }</c> は <c>body.orderId</c>（path）と <c>body.currency</c>（literal）になる。
    /// ドットキーで書いた場合と同じフラットな <c>values</c> になり、実行時は同じ論理ツリーに組み上がる。
    /// </para>
    /// <para>
    /// 展開後の論理キーは、大文字小文字を区別せずに重複と競合（あるキーが別キーのドット区切りの親になる。例: <c>ship</c> と <c>ship.address</c>）を検査し、どちらも拒否する。
    /// </para>
    /// </remarks>
    /// <param name="map">キーと値の input 辞書。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>各論理キーを path または literal として解釈した定義。</returns>
    /// <exception cref="ArgumentException">不正なパス・テンプレート・空キー・配列の中のパス式・論理キーの重複や競合があるとき。</exception>
    private static StateInputDefinition ParseStrictInputFromMap(
        Dictionary<string, object?> map,
        string? ownerLabel)
    {
        if (map.Keys.Any(key => string.Equals(key, "retry", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(Format(ownerLabel, "'retry' must not appear inside 'input'; declare it as a sibling of 'action'."));
        }

        var values = new Dictionary<string, StateInputValueDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (logicalKey, value) in map.SelectMany(entry => FlattenStrictInputEntry(entry.Key, entry.Value, ownerLabel)))
        {
            if (!values.TryAdd(logicalKey, value))
            {
                throw new ArgumentException(Format(ownerLabel, $"duplicate input key '{logicalKey}'."));
            }
        }

        RejectConflictingInputKeys(values.Keys, ownerLabel);
        return new StateInputDefinition { Values = values };
    }

    /// <summary>
    /// input マップの 1 エントリを、論理キー（ドットキー）と値定義の組へ展開する。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>文字列: <see cref="ParseStrictInputValue"/> で path または literal にする。</description></item>
    /// <item><description>空でない map: 子キーごとに <c>論理キー.子キー</c> で再帰する。子キーにドットがあれば、そのままネストの区切りになる。</description></item>
    /// <item><description>配列: 中（入れ子の map・配列を含む）にパス式の形の文字列があれば拒否し、無ければ配列ごと literal にする。</description></item>
    /// <item><description>空の map・数値・真偽値・null: literal にする。</description></item>
    /// </list>
    /// </remarks>
    /// <param name="logicalKey">このエントリの論理キー（トップレベルではマップのキー）。</param>
    /// <param name="raw">YAML/JSON 由来の値。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>論理キーと値定義の組の列。</returns>
    /// <exception cref="ArgumentException">不正なパス・テンプレート・空の子キー・配列の中のパス式があるとき。</exception>
    private static IEnumerable<KeyValuePair<string, StateInputValueDefinition>> FlattenStrictInputEntry(
        string logicalKey,
        object? raw,
        string? ownerLabel) =>
        raw switch
        {
            string => [new(logicalKey, ParseStrictInputValue(raw, logicalKey, ownerLabel))],
            IDictionary { Count: > 0 } nested => ToStringDict(nested).SelectMany(child =>
                FlattenStrictInputEntry(JoinChildKey(logicalKey, child.Key, ownerLabel), child.Value, ownerLabel)),
            IEnumerable sequence and not IDictionary => [new(logicalKey, ParseStrictInputArray(sequence, logicalKey, ownerLabel))],
            _ => [new(logicalKey, new StateInputValueDefinition { Literal = raw })]
        };

    /// <summary>ネスト map の子キーを親の論理キーへ連結する。</summary>
    /// <param name="parentKey">親の論理キー。</param>
    /// <param name="childKey">子キー。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns><c>親.子</c> の論理キー。</returns>
    /// <exception cref="ArgumentException">子キーが空または空白だけのとき。</exception>
    private static string JoinChildKey(string parentKey, string childKey, string? ownerLabel) =>
        string.IsNullOrWhiteSpace(childKey)
            ? throw new ArgumentException(Format(ownerLabel, $"input key '{parentKey}' contains an empty child key."))
            : $"{parentKey}.{childKey}";

    /// <summary>
    /// 配列の input 値を literal にする。配列の中のパス式は評価できないため拒否する。
    /// </summary>
    /// <param name="sequence">配列の値。</param>
    /// <param name="logicalKey">配列の論理キー。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>配列をそのまま持つ literal の値定義。</returns>
    /// <exception cref="ArgumentException">配列の中にパス式の形の文字列があるとき（メッセージに <c>items[0]</c> の形の論理パスを示す）。</exception>
    private static StateInputValueDefinition ParseStrictInputArray(IEnumerable sequence, string logicalKey, string? ownerLabel)
    {
        if (FindPathExpressionInArray(sequence, logicalKey) is { } location)
        {
            throw new ArgumentException(Format(
                ownerLabel,
                $"input key '{location}': path expressions are not allowed inside arrays."));
        }

        return new StateInputValueDefinition { Literal = sequence };
    }

    /// <summary>配列（入れ子の map・配列を含む）の中で最初に見つかったパス式の論理パスを返す。</summary>
    /// <param name="value">走査する値。</param>
    /// <param name="location">この値の論理パス（例: <c>lines[0].sku</c>）。</param>
    /// <returns>パス式があればその論理パス。無ければ null。</returns>
    private static string? FindPathExpressionInArray(object? value, string location) =>
        value switch
        {
            string text => IsPathExpression(text) ? location : null,
            IDictionary map => ToStringDict(map)
                .Select(child => FindPathExpressionInArray(child.Value, $"{location}.{child.Key}"))
                .FirstOrDefault(found => found is not null),
            IEnumerable sequence => sequence.Cast<object?>()
                .Select((item, index) => FindPathExpressionInArray(item, $"{location}[{index}]"))
                .FirstOrDefault(found => found is not null),
            _ => null
        };

    /// <summary>
    /// あるキーが別キーのドット区切りの親になっている組（例: <c>ship</c> と <c>ship.address</c>）を拒否する。
    /// </summary>
    /// <remarks>セグメント単位で比べるので、<c>ship</c> と <c>shipping.x</c> は競合としない。</remarks>
    /// <param name="keys">展開後の論理キー（重複は除去済み）。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <exception cref="ArgumentException">競合する組があるとき。</exception>
    private static void RejectConflictingInputKeys(IEnumerable<string> keys, string? ownerLabel)
    {
        var keySet = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflict = keySet
            .SelectMany(key => EnumerateDottedParents(key).Select(parent => (Parent: parent, Child: key)))
            .FirstOrDefault(pair => keySet.Contains(pair.Parent));
        if (conflict.Parent is not null)
        {
            throw new ArgumentException(Format(ownerLabel, $"input key '{conflict.Parent}' conflicts with '{conflict.Child}'."));
        }
    }

    /// <summary>ドットキーの親キーを列挙する（<c>a.b.c</c> なら <c>a</c>、<c>a.b</c>）。</summary>
    /// <param name="key">論理キー。</param>
    /// <returns>親キーの列。</returns>
    private static IEnumerable<string> EnumerateDottedParents(string key) =>
        key.Select((character, index) => (character, index))
            .Where(pair => pair.character == '.' && pair.index > 0)
            .Select(pair => key[..pair.index]);

    /// <summary><c>workflow.modules</c> を syntax parse する（semantic resolution は行わない）。</summary>
    /// <param name="workflowDict"><c>workflow</c> ブロックの辞書。</param>
    /// <returns>module alias → import 宣言。未指定時は null。</returns>
    protected static IReadOnlyDictionary<string, ModuleImportReference>? ParseWorkflowModules(Dictionary<string, object?> workflowDict)
    {
        ArgumentNullException.ThrowIfNull(workflowDict);
        if (!workflowDict.TryGetValue("modules", out var modulesVal) || modulesVal is null)
        {
            return null;
        }

        var modulesDict = ToStringDict(modulesVal);
        if (modulesDict.Count == 0)
        {
            return null;
        }

        var entries = modulesDict
            .Select(entry => new KeyValuePair<string, string>(
                entry.Key,
                entry.Value?.ToString() ?? string.Empty))
            .ToList();

        var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modules = new Dictionary<string, ModuleImportReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var (aliasRaw, importValueRaw) in entries)
        {
            var alias = aliasRaw.Trim();
            if (alias.Length == 0)
            {
                throw new ArgumentException("workflow.modules contains an empty alias key.");
            }

            if (!IdentifierCharset.IsValid(alias))
            {
                throw new ArgumentException($"workflow.modules alias '{alias}' must be an ASCII identifier.");
            }

            if (!seenAliases.Add(alias))
            {
                throw new ArgumentException($"workflow.modules contains duplicate alias '{alias}'.");
            }

            var importValue = importValueRaw.Trim();
            if (importValue.Length == 0)
            {
                throw new ArgumentException($"workflow.modules['{alias}'] requires a non-empty ModuleId.");
            }

            modules[alias] = ModuleImportReference.ParseImportValue(importValue);
        }

        return modules;
    }

    /// <summary>状態／ノード直下の <c>retry</c> ブロックを syntax parse する。</summary>
    /// <param name="dict">状態またはノード辞書。</param>
    /// <returns>retry 定義。未指定時は null。</returns>
    protected static RetryDefinition? ParseRetryDefinition(Dictionary<string, object?> dict)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue("retry", out var retryVal) || retryVal is null)
        {
            return null;
        }

        var retryDict = ToStringDict(retryVal);
        if (retryDict.Count == 0)
        {
            return null;
        }

        return new RetryDefinition
        {
            Limit = GetNullableInt(retryDict, "limit"),
            Backoff = GetStr(retryDict, "backoff"),
            Errors = GetStrList(retryDict, "errors"),
        };
    }

    /// <summary>input マップの 1 エントリを path または literal として解釈する。</summary>
    /// <param name="raw">YAML/JSON 由来の値。</param>
    /// <param name="key">マップ上のキー（エラーメッセージ用）。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <returns>path または literal を設定した値定義。</returns>
    private static StateInputValueDefinition ParseStrictInputValue(
        object? raw,
        string key,
        string? ownerLabel)
    {
        if (raw is not string str)
        {
            return new StateInputValueDefinition { Literal = raw };
        }

        RejectTemplate(ownerLabel, str);
        if (!IsPathExpression(str))
        {
            return new StateInputValueDefinition { Literal = str };
        }

        EnsureReadPath(str, ownerLabel, $"invalid input path for key '{key}': '{str}'");
        return new StateInputValueDefinition { Path = str };
    }

    /// <summary>
    /// <paramref name="path"/> が読み取りパス（許可された CallPath、または SimpleJsonPath）でなければ例外を投げる。
    /// </summary>
    /// <param name="path">パス式。</param>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <param name="message">末尾の句点を除いたエラーメッセージ。CallPath の誤りには許可一覧を添える。</param>
    /// <exception cref="ArgumentException">読み取りパスとして受理できないとき。</exception>
    private static void EnsureReadPath(string path, string? ownerLabel, string message)
    {
        var detail = ExecutionContextReadPath.Classify(path) switch
        {
            ReadPathValidity.Valid => null,
            ReadPathValidity.InvalidCallPath => $"{message} ({ExecutionContextReadPath.FormatAllowedCallPathsHint()}).",
            _ => $"{message}."
        };
        if (detail is not null)
        {
            throw new ArgumentException(Format(ownerLabel, detail));
        }
    }

    /// <summary>
    /// YAML の <c>when</c> オブジェクト（<c>path</c> / <c>op</c> / <c>value</c>）から条件式を構築する。
    /// <c>path</c> は <see cref="ExecutionContextReadPath.Classify"/> で検証する（許可された CallPath も受理する）。
    /// </summary>
    /// <param name="whenDict"><c>when</c> の辞書。</param>
    /// <param name="ownerLabel">エラーメッセージ用（例: nodes のノード id）。省略時はメッセージのみ。</param>
    /// <returns>構築した条件式。</returns>
    protected static ConditionExpressionDefinition ParseConditionWhen(
        Dictionary<string, object?> whenDict,
        string? ownerLabel = null)
    {
        ArgumentNullException.ThrowIfNull(whenDict);

        var path = GetStr(whenDict, "path");
        var op = GetStr(whenDict, "op");

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(Format(ownerLabel, "when requires non-empty 'path'."));
        }

        EnsureReadPath(path, ownerLabel, $"invalid when.path: '{path}'");

        if (string.IsNullOrWhiteSpace(op))
        {
            throw new ArgumentException(Format(ownerLabel, "when requires non-empty 'op'."));
        }

        whenDict.TryGetValue("value", out var value);
        return new ConditionExpressionDefinition
        {
            Path = path,
            Op = op,
            Value = value
        };
    }

    /// <summary>
    /// 辞書の子キーに対応する値をネスト辞書として取得する。欠損・null のときは空辞書。
    /// </summary>
    /// <param name="dict">親辞書。</param>
    /// <param name="key">子を指すキー。</param>
    /// <param name="comparer">キー比較子。省略時は序数無視比較。</param>
    /// <returns>正規化した子辞書。</returns>
    protected static Dictionary<string, object?> GetChildDict(
        Dictionary<string, object?> dict,
        string key,
        IEqualityComparer<string>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var val) || val == null)
        {
            return new Dictionary<string, object?>(comparer ?? StringComparer.Ordinal);
        }

        return ToStringDict(val, comparer);
    }

    /// <summary>
    /// 任意オブジェクトを <see cref="string"/> キーの辞書へ正規化する。
    /// </summary>
    /// <param name="val">YAML/JSON 由来のオブジェクト。</param>
    /// <param name="comparer">キー比較子。省略時は序数無視比較。</param>
    /// <returns>正規化した辞書。</returns>
    protected static Dictionary<string, object?> ToStringDict(object? val, IEqualityComparer<string>? comparer = null)
    {
        var cmp = comparer ?? StringComparer.Ordinal;
        var result = new Dictionary<string, object?>(cmp);
        if (val == null)
        {
            return result;
        }

        if (val is Dictionary<string, object?> strDict)
        {
            foreach (var (key, value) in strDict)
            {
                result[key] = value;
            }

            return result;
        }

        if (val is IDictionary dict)
        {
            foreach (DictionaryEntry kv in dict)
            {
                result[kv.Key?.ToString() ?? ""] = kv.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 辞書の値を文字列として取得する。欠損・null のときは null。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>文字列化した値、または null。</returns>
    protected static string? GetStr(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        return dict.TryGetValue(key, out var v) && v != null ? v.ToString() : null;
    }

    /// <summary>
    /// 辞書の値を真偽として解釈する。欠損・null・非対応型のときは false。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>真偽値。</returns>
    protected static bool GetBool(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var v) || v == null)
        {
            return false;
        }

        if (v is bool b)
        {
            return b;
        }

        return string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 辞書の値を <see cref="int"/> に変換する。未設定・非対応型・パース不能のときは null。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>変換できた整数、または null。</returns>
    protected static int? GetNullableInt(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => checked((int)l),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// 辞書の値を文字列の列として取得する。配列・列挙でない、または欠損のときは null。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">読み取るキー。</param>
    /// <returns>文字列の一覧、または null。</returns>
    protected static IReadOnlyList<string>? GetStrList(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue(key, out var v) || v == null)
        {
            return null;
        }

        if (v is IEnumerable enumerable && v is not string)
        {
            return enumerable.Cast<object?>().Select(x => x?.ToString() ?? "").ToList();
        }

        return null;
    }

    /// <summary>
    /// 辞書にキーが存在するかを序数無視で判定する。
    /// </summary>
    /// <param name="dict">キーと値の辞書。</param>
    /// <param name="key">検索するキー。</param>
    /// <returns>存在するとき true。</returns>
    protected static bool HasKeyIgnoreCase(Dictionary<string, object?> dict, string key)
    {
        ArgumentNullException.ThrowIfNull(dict);
        return dict.Keys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>文字列が簡易 JSONPath 式（<c>$</c> または <c>$.</c> 始まり）かどうかを判定する。</summary>
    /// <param name="s">判定対象の文字列。</param>
    /// <returns>パス式として扱うとき true。</returns>
    private static bool IsPathExpression(string s) => SimpleJsonPath.IsPathExpression(s);

    /// <summary><c>${...}</c> 形式のテンプレート文字列を拒否する（厳格仕様では未サポート）。</summary>
    /// <param name="ownerLabel">エラーメッセージ用のオーナー文脈。省略可。</param>
    /// <param name="value">検査対象の文字列。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> が <c>${</c> で始まるとき。</exception>
    private static void RejectTemplate(string? ownerLabel, string value)
    {
        if (!value.StartsWith("${", StringComparison.Ordinal))
        {
            return;
        }

        throw new ArgumentException(Format(ownerLabel, "'${...}' input templates are not supported; use $. paths only."));
    }

    /// <summary>
    /// ローダー由来の例外メッセージにオーナー文脈（ノード id 等）を付与する。
    /// </summary>
    protected static string Format(string? ownerLabel, string message) =>
        string.IsNullOrWhiteSpace(ownerLabel) ? message : $"Node '{ownerLabel}': {message}";
}
