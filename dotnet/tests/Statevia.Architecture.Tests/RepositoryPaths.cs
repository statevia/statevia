namespace Statevia.Architecture.Tests;

/// <summary>
/// テストからリポジトリ内のファイルを読むためのパスを解決する。
/// </summary>
/// <remarks>
/// テストはビルド出力（<c>bin/</c>）で動くので、実行ディレクトリから親をたどって <c>dotnet/statevia.sln</c> を探す。
/// docs やソースをリポジトリから直接読むテスト（EventId の採番、文書と実装の照合）が共有する。
/// </remarks>
internal static class RepositoryPaths
{
    private static readonly Lazy<string> Root = new(FindRepositoryRoot);

    /// <summary>リポジトリルートの絶対パス。</summary>
    /// <exception cref="DirectoryNotFoundException">ルートが見つからないとき（初回の参照で投げる）。</exception>
    public static string RepositoryRoot => Root.Value;

    /// <summary>リポジトリ相対パス（区切りは <c>/</c>）を絶対パスにする。</summary>
    /// <param name="relativePath">リポジトリルートからの相対パス。</param>
    /// <returns>絶対パス。</returns>
    public static string Resolve(string relativePath) =>
        Path.Combine([RepositoryRoot, .. relativePath.Split('/')]);

    /// <summary>
    /// テスト実行ディレクトリから親をたどり、<c>dotnet/statevia.sln</c> があるリポジトリルートを返す。
    /// </summary>
    /// <returns>リポジトリルートの絶対パス。</returns>
    /// <exception cref="DirectoryNotFoundException">ルートが見つからないとき。</exception>
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dotnet", "statevia.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("dotnet/statevia.sln を含むリポジトリルートが見つかりません。");
    }
}
