using System.Diagnostics;

namespace Statevia.Tools.Capacity;

/// <summary>計測対象リポジトリの git SHA を取得する。</summary>
internal static class GitShaReader
{
    /// <summary>
    /// <c>git rev-parse HEAD</c> の結果。失敗時は空文字（公開結果に秘密を出さない）。
    /// </summary>
    /// <param name="workingDirectory">git ルート候補。</param>
    /// <returns>40 文字 SHA または空。</returns>
    public static string TryRead(string workingDirectory)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            if (process.ExitCode != 0 || output.Length is < 7 or > 64)
            {
                return string.Empty;
            }

            return output;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException)
        {
            return string.Empty;
        }
    }
}
