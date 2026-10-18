using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Definition.Validation;
using System.Collections;

namespace Statevia.Service.Api.Application.Definition;

/// <summary>Nodes 形式の <c>bindings</c> を参照マップとして読む。</summary>
/// <remarks>States 形式はエンジン単体の定義であり、このキーを解釈しない。</remarks>
internal static class NodeActionBindingSyntax
{
    /// <summary>1 状態のスロット上限。1 回の実行リクエストに載せる件数。</summary>
    public const int MaxSlotsPerState = 16;

    /// <summary>スロット名と参照名の最大長。イベント名と同じ ASCII 識別子長。</summary>
    public const int MaxIdentifierLength = 64;

    /// <summary>
    /// ノード辞書の <c>bindings</c> を読む。キーが無ければ null。
    /// </summary>
    /// <param name="dict">ノード辞書。</param>
    /// <param name="action">ノードの action。省略時は null。</param>
    /// <param name="hasWaitOrJoin"><c>wait</c> または <c>join</c> があるか。</param>
    /// <param name="ownerLabel">エラーメッセージ用のノード名。</param>
    /// <returns>参照マップ。キーが無ければ null。</returns>
    /// <exception cref="ArgumentException">識別子違反、空、スロット超過、wait / join 併記、action 省略。</exception>
    public static ActionBindingReferences? Parse(
        Dictionary<string, object?> dict,
        string? action,
        bool hasWaitOrJoin,
        string? ownerLabel)
    {
        ArgumentNullException.ThrowIfNull(dict);
        if (!dict.TryGetValue("bindings", out var raw) || raw is null)
        {
            return null;
        }

        if (raw is not IDictionary)
        {
            throw new ArgumentException(Format(ownerLabel, "bindings must be a map of secrets and variables."));
        }

        var map = ToStringDict(raw, StringComparer.OrdinalIgnoreCase);
        var unknown = map.Keys.FirstOrDefault(key =>
            !string.Equals(key, "secrets", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(key, "variables", StringComparison.OrdinalIgnoreCase));
        if (unknown is not null)
        {
            throw new ArgumentException(Format(ownerLabel, $"bindings has unknown key '{unknown}'."));
        }

        var secrets = ParseNameMap(map, "secrets", ownerLabel);
        var variables = ParseNameMap(map, "variables", ownerLabel);
        var slotCount = secrets.Count + variables.Count;
        if (slotCount == 0)
        {
            throw new ArgumentException(Format(ownerLabel, "bindings must name at least one slot."));
        }

        if (slotCount > MaxSlotsPerState)
        {
            throw new ArgumentException(Format(ownerLabel, $"bindings allows at most {MaxSlotsPerState} slots."));
        }

        var duplicateSlot = secrets.Keys
            .Concat(variables.Keys)
            .GroupBy(slot => slot, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateSlot is not null)
        {
            throw new ArgumentException(Format(ownerLabel, $"bindings slot '{duplicateSlot}' is duplicated."));
        }

        if (hasWaitOrJoin)
        {
            throw new ArgumentException(Format(ownerLabel, "bindings cannot be combined with wait or join."));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException(Format(ownerLabel, "bindings requires an action."));
        }

        return new ActionBindingReferences(secrets, variables);
    }

    private static Dictionary<string, string> ParseNameMap(
        Dictionary<string, object?> bindings,
        string kindKey,
        string? ownerLabel)
    {
        if (!bindings.TryGetValue(kindKey, out var raw) || raw is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (raw is not IDictionary)
        {
            throw new ArgumentException(Format(ownerLabel, $"bindings.{kindKey} must be a map."));
        }

        var map = ToStringDict(raw, StringComparer.Ordinal);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (slot, value) in map)
        {
            if (value is not string referenceName)
            {
                throw new ArgumentException(Format(ownerLabel, $"bindings.{kindKey}.{slot} must be a binding name."));
            }

            if (!IsIdentifier(slot) || !IsIdentifier(referenceName))
            {
                throw new ArgumentException(
                    Format(ownerLabel, $"bindings.{kindKey}.{slot} must use identifiers of 1 to {MaxIdentifierLength} characters."));
            }

            names[slot] = referenceName;
        }

        return names;
    }

    private static bool IsIdentifier(string value) =>
        value.Length is > 0 && value.Length <= MaxIdentifierLength && IdentifierCharset.IsValid(value);

    private static string Format(string? ownerLabel, string message) =>
        string.IsNullOrWhiteSpace(ownerLabel) ? message : $"Node '{ownerLabel}': {message}";

    private static Dictionary<string, object?> ToStringDict(object? value, IEqualityComparer<string> comparer)
    {
        var result = new Dictionary<string, object?>(comparer);
        if (value is not IDictionary dict)
        {
            return result;
        }

        foreach (DictionaryEntry entry in dict)
        {
            result[entry.Key?.ToString() ?? ""] = entry.Value;
        }

        return result;
    }
}
