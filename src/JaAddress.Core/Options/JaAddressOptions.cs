namespace JaAddress.Core.Options;

public sealed class JaAddressOptions {
    /// <summary>DataBuilderが出力したデータディレクトリのパス</summary>
    public string DataDirectory { get; init; } = string.Empty;
}
