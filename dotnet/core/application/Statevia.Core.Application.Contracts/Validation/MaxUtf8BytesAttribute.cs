using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>
/// 文字列の UTF-8 バイト数が上限以下であることを検証する属性。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MaxLengthAttribute"/> は UTF-16 の文字数を数えるため、マルチバイト文字を含む入力のバイト上限には使えない。
/// 本属性は <see cref="Encoding.UTF8"/> のバイト数で判定する。
/// </para>
/// <para>
/// <c>null</c> は本属性では検証しない（必須は <see cref="RequiredAttribute"/> に委譲）。
/// 非 <see cref="string"/> は検証対象外（成功扱い）。
/// </para>
/// </remarks>
/// <param name="maxBytes">許容する最大 UTF-8 バイト数。1 以上。</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
public sealed class MaxUtf8BytesAttribute(int maxBytes) : ValidationAttribute("The field {0} exceeds the maximum UTF-8 byte length.")
{
    /// <summary>許容する最大 UTF-8 バイト数。</summary>
    public int MaxBytes { get; } = maxBytes > 0
        ? maxBytes
        : throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "maxBytes must be positive.");

    /// <inheritdoc />
    public override bool IsValid(object? value)
        => value is not string text || Encoding.UTF8.GetByteCount(text) <= MaxBytes;
}
