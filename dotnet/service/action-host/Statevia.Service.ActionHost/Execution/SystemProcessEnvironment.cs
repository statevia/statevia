using System.Collections;

namespace Statevia.Service.ActionHost.Execution;

/// <summary><see cref="EnvironmentVariableTarget.Process"/> だけを読み書きする。</summary>
internal sealed class SystemProcessEnvironment : IProcessEnvironment
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Read()
    {
        var values = new Dictionary<string, string>(ProcessEnvironmentNames.Comparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process))
        {
            if (entry.Key is string name && entry.Value is string value)
            {
                values[name] = value;
            }
        }

        return values;
    }

    /// <inheritdoc />
    public void Set(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
}
