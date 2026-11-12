using System.Text.Json.Serialization;

namespace JaAddress.DataBuilder.Models;

public sealed class TownRootResponse {
    public TownMeta Meta { get; init; } = new();
    public List<TownEntry> Data { get; init; } = [];
}

public sealed class TownMeta {
    public long Updated { get; init; }
}

public sealed class TownEntry {
    public string MachiazaId { get; init; } = string.Empty;

    [JsonPropertyName("oaza_cho")]
    public string? OazaCho { get; init; }

    [JsonPropertyName("oaza_cho_k")]
    public string? OazaChoK { get; init; }

    [JsonPropertyName("chome")]
    public string? Chome { get; init; }

    [JsonPropertyName("chome_n")]
    public int? ChomeN { get; init; }

    [JsonPropertyName("koaza")]
    public string? Koaza { get; init; }

    public bool Rsdt { get; init; }
    public double[]? Point { get; init; }
}

// namespace JaAddress.DataBuilder.Models;

// public sealed class TownRootResponse {
//     public TownMeta Meta { get; init; } = new();
//     public List<TownEntry> Data { get; init; } = [];
// }

// public sealed class TownMeta {
//     public long Updated { get; init; }
// }

// public sealed class TownEntry {
//     public string MachiazaId { get; init; } = string.Empty;
//     public string? OazaCho { get; init; }       // 大字・町名
//     public string? Chome { get; init; }          // 丁目
//     public int? ChomeN { get; init; }            // 丁目番号
//     public string? Koaza { get; init; }          // 小字
//     public double[]? Point { get; init; }
// }
