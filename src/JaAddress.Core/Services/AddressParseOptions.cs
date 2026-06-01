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
}
