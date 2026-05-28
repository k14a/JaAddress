namespace JaAddress.Core.Services;

public sealed class AddressParseOptions {
    /// <summary>町字・Street・Blockまで分割するか（falseならRemainderのみ）</summary>
    public bool SplitRemainder { get; init; } = false;

    /// <summary>数字を正規化するか（全角→半角）</summary>
    public bool NormalizeNumber { get; init; } = true;
}
