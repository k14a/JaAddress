namespace JaAddress.Core.Services;

/// <summary>
/// <see cref="IAddressService.ParseWithReasonAsync"/> の結果。
/// 解析に成功した場合は <see cref="Result"/> が非 null で <see cref="FailureReason"/> は
/// <see cref="AddressParseFailureReason.None"/>、失敗した場合は <see cref="Result"/> が null で
/// <see cref="FailureReason"/> に失敗理由が入る。
/// </summary>
public sealed class AddressParseOutcome {
    /// <summary>解析結果。解析できなかった場合は null。</summary>
    public AddressParseResult? Result { get; init; }
    /// <summary>解析できなかった理由。成功した場合は <see cref="AddressParseFailureReason.None"/>。</summary>
    public AddressParseFailureReason FailureReason { get; init; }

    /// <summary>成功の結果を作る。</summary>
    /// <param name="result">解析結果。</param>
    public static AddressParseOutcome Success(AddressParseResult result) =>
        new() { Result = result, FailureReason = AddressParseFailureReason.None };

    /// <summary>失敗の結果を作る。</summary>
    /// <param name="reason">解析できなかった理由。</param>
    public static AddressParseOutcome Failure(AddressParseFailureReason reason) =>
        new() { Result = null, FailureReason = reason };
}
