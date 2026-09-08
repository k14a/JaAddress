namespace JaAddress.Api.Models;

public sealed class ParseRequest {
    public IReadOnlyList<string> Addresses { get; init; } = [];
    public bool BestEffort { get; init; } = false;
    /// <summary>true の場合、町字・Street・Block まで分割する。</summary>
    public bool SplitRemainder { get; init; } = true;
    /// <summary>全角数字・ハイフン類を半角に正規化するか。</summary>
    public bool NormalizeNumber { get; init; } = true;
    /// <summary>
    /// true の場合、「大字」が省略された入力でも「大字XXX」データと照合し、
    /// 正規形（大字付き）の町字名を返す。
    /// </summary>
    public bool NormalizeOaza { get; init; } = true;

    /// <summary>
    /// このリクエストで異体字（旧字体）の畳み込みを行うか。null（既定）はサーバ設定
    /// （JaAddress:FoldItaiji）に従う。サーバ設定が false のときは true にしても効果はない。
    /// </summary>
    public bool? FoldItaiji { get; init; }
}
