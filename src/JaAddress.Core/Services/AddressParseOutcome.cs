namespace JaAddress.Core.Services;

/// <summary>
/// <see cref="IAddressService.ParseWithReasonAsync"/> の結果。
/// 解析に成功した場合は <see cref="Result"/> が非 null で <see cref="FailureReason"/> は
/// <see cref="AddressParseFailureReason.None"/>、失敗した場合は <see cref="Result"/> が null で
/// <see cref="FailureReason"/> に失敗理由が入る。
/// </summary>
public sealed class AddressParseOutcome {
    public AddressParseResult? Result { get; init; }
    public AddressParseFailureReason FailureReason { get; init; }

    public static AddressParseOutcome Success(AddressParseResult result) =>
        new() { Result = result, FailureReason = AddressParseFailureReason.None };

    public static AddressParseOutcome Failure(AddressParseFailureReason reason) =>
        new() { Result = null, FailureReason = reason };
}
