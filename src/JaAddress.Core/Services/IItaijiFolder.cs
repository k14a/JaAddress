namespace JaAddress.Core.Services;

/// <summary>
/// 異体字（旧字体・許容字体）を正規形（新字体）へ畳み込むためのインターフェース。
/// <see cref="AddressService"/> が住所照合前に入力・辞書名称の双方へ適用する。
/// 外部からも DI で取得でき、呼び出し側で同じマップを再利用できる
/// （例: DB の未正規化住所カラムに対する異体字バリアント検索）。
/// </summary>
public interface IItaijiFolder {
    /// <summary>
    /// 畳み込みが有効か（<see cref="Options.JaAddressOptions.FoldItaiji"/> が true かつマップが 1 件以上）。
    /// </summary>
    bool Enabled { get; }

    /// <summary>変換元文字 → 変換先文字のマップ（読み取り専用）。</summary>
    IReadOnlyDictionary<char, char> Map { get; }

    /// <summary>
    /// 入力文字列中の異体字を正規形へ置換して返す。
    /// <see cref="Enabled"/> が false の場合、またはヒットする文字が無い場合は
    /// 引数をそのまま返す。1 文字 → 1 文字の置換のため、文字列長は変化しない。
    /// </summary>
    string Fold(string value);
}
