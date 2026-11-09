namespace Statevia.Infrastructure.Modules;

/// <summary>Module load 結果の読み取り専用カタログ。</summary>
internal sealed class ModuleLoadCatalog
{
    private readonly object _sync = new();
    private readonly List<ModuleLoadRecord> _records = [];

    /// <summary>登録済みレコードのスナップショットを返す。</summary>
    /// <returns>呼び出し時点のレコードのコピー。後の <see cref="Upsert"/> の影響を受けない。</returns>
    public IReadOnlyList<ModuleLoadRecord> GetRecords()
    {
        lock (_sync)
        {
            return _records.ToList();
        }
    }

    /// <summary>load 結果を追記する（同一 entry パスは上書き更新）。</summary>
    /// <param name="record">追記するレコード。</param>
    public void Upsert(ModuleLoadRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_sync)
        {
            var index = _records.FindIndex(
                existing => EntryAssemblyPathEquals(existing.EntryAssemblyPath, record.EntryAssemblyPath));
            if (index >= 0)
            {
                _records[index] = record;
                return;
            }

            _records.Add(record);
        }
    }

    /// <summary>entry パスが Loaded のレコードとして存在するか。</summary>
    /// <param name="entryAssemblyPath">entry DLL 絶対パス。大文字小文字を区別せずに比べる。</param>
    /// <returns>同じ entry パスのレコードがあり、状態が <see cref="ModuleLoadStatus.Loaded"/> なら <see langword="true"/>。</returns>
    public bool IsLoaded(string entryAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryAssemblyPath);

        lock (_sync)
        {
            return _records.Any(
                record => EntryAssemblyPathEquals(record.EntryAssemblyPath, entryAssemblyPath)
                    && record.Status == ModuleLoadStatus.Loaded);
        }
    }

    private static bool EntryAssemblyPathEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
