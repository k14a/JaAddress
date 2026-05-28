namespace JaAddress.Api.Models;

public sealed class ParseResponse {
    public IReadOnlyList<ParseResultDto> Results { get; init; } = [];
    public int Count { get; init; }
}

