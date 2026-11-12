namespace JaAddress.Api.Options;

public sealed class ApiOptions {
    public int MaxJsonBatchSize { get; init; } = 20;
    public int MaxTsvRows { get; init; } = 10000;
}
