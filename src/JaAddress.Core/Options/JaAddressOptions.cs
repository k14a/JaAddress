namespace JaAddress.Core.Options;

public sealed class JaAddressOptions {
    /// <summary>DataBuilderが出力したデータディレクトリのパス</summary>
    public string DataDirectory { get; init; } = string.Empty;

    /// <summary>
    /// true の場合、異体字（旧字体・許容字体）を正規形（新字体）へ畳み込んでから住所照合を行う。
    /// 入力住所文字列と住所辞書（都道府県・市区町村・町字）の名称の双方に適用されるため、
    /// 「須惠町」(惠) と「須恵町」(恵) のような字体の揺れを吸収できる。
    /// 既定は false（オプトイン）。有効時は City.Name / Town.Name も畳み込み後の表記で返る。
    /// </summary>
    public bool FoldItaiji { get; init; }

    /// <summary>
    /// 追加の異体字マップ TSV ファイルのパス（任意）。既定の埋め込みマップにマージされ、既定より優先される。
    /// 書式は Resources/itaiji.tsv を参照（<c>変換元&lt;TAB&gt;変換先</c>、1 文字 → 1 文字）。
    /// </summary>
    public string? ItaijiMapPath { get; init; }

    /// <summary>
    /// コードから追加する異体字マップ（任意）。既定マップおよび <see cref="ItaijiMapPath"/> よりさらに優先される。
    /// </summary>
    public IReadOnlyDictionary<char, char>? AdditionalItaiji { get; init; }
}
