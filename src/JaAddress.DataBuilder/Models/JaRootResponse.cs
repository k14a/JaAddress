namespace JaAddress.DataBuilder.Models;

/// <summary>
/// ja.json のルートレスポンス
/// </summary>
public sealed class JaRootResponse {
    public JaMeta Meta { get; init; } = new();
    public List<JaPrefecture> Data { get; init; } = [];
}

public sealed class JaMeta {
    public long Updated { get; init; }
}

/// <summary>
/// 都道府県エントリ
/// </summary>
public sealed class JaPrefecture {
    public int Code { get; init; }
    public string Pref { get; init; } = string.Empty;
    public double[] Point { get; init; } = [];
    public List<JaCity> Cities { get; init; } = [];
}

/// <summary>
/// 市区町村エントリ
/// </summary>
public sealed class JaCity {
    public int Code { get; init; }
    public string? County { get; init; }
    public string City { get; init; } = string.Empty;
    public string? Ward { get; init; }
    public double[] Point { get; init; } = [];
}
