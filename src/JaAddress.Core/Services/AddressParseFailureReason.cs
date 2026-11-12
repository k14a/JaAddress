namespace JaAddress.Core.Services;

/// <summary>
/// <see cref="IAddressService.ParseWithReasonAsync"/> で住所を解析できなかった理由。
/// </summary>
public enum AddressParseFailureReason {
    /// <summary>解析に成功した</summary>
    None,
    /// <summary>
    /// 都道府県を特定できなかった。
    /// BestEffort=false の場合は入力の先頭が都道府県名で始まらない、
    /// BestEffort=true の場合は入力中に都道府県名が1つも出現しない。
    /// </summary>
    PrefectureNotFound,
    /// <summary>
    /// 都道府県は特定できたが、市区町村を特定できなかった（区名・市区町村名・郡名の省略補正を含む）。
    /// BestEffort=true で都道府県名が複数出現する場合は、すべての出現位置で市区町村を特定できなかった。
    /// </summary>
    CityNotFound,
}
