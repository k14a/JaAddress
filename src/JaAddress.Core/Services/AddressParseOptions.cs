namespace JaAddress.Core.Services;

public sealed class AddressParseOptions {
    /// <summary>町字・Street・Blockまで分割するか（falseならRemainderのみ）</summary>
    public bool SplitRemainder { get; init; } = false;

    /// <summary>数字を正規化するか（全角→半角）</summary>
    public bool NormalizeNumber { get; init; } = true;
    /// <summary>
    /// true の場合、入力文字列の先頭以外に都道府県名が見つかった位置から解析を試みる。
    /// 例: "勤務地：東京都港区…" → Offset=4 で "東京都港区…" を解析。
    /// </summary>
    public bool BestEffort { get; init; } = false;

    /// <summary>
    /// true の場合、「大字」が省略されたまちあざ名でも「大字XXX」データと照合する。
    /// 一致した場合は正規形（大字付き）の名前を返す。
    /// 例: 入力 "久保" → データ "大字久保" に一致し、Town.Name = "大字久保" を返す。
    /// </summary>
    public bool NormalizeOaza { get; init; } = true;

    /// <summary>
    /// このリクエストで入力文字列の異体字畳み込みを行うか。
    /// null（既定）の場合は <see cref="Options.JaAddressOptions.FoldItaiji"/> に従う。
    /// <see cref="Options.JaAddressOptions.FoldItaiji"/> が false のときは辞書側が畳み込まれていないため、
    /// ここで true にしても効果はない（false で明示的に無効化する用途のみ）。
    /// </summary>
    public bool? FoldItaiji { get; init; }
}
