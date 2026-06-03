namespace JaAddress.Api.Models;

public sealed class ParseRequest {
    public IReadOnlyList<string> Addresses { get; init; } = [];
    public bool BestEffort { get; init; } = false;
    /// <summary>
    /// true の場合、「大字」が省略された入力でも「大字XXX」データと照合し、
    /// 正規形（大字付き）の町字名を返す。
    /// </summary>
    public bool NormalizeOaza { get; init; } = true;
}
