namespace JaAddress.Api.Models;

public sealed class ParseRequest {
    public IReadOnlyList<string> Addresses { get; init; } = [];
}
