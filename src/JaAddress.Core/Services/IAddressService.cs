using JaAddress.Core.Models;

namespace JaAddress.Core.Services;

public interface IAddressService {
    Task<IReadOnlyList<Prefecture>> GetPrefecturesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<City>> GetCitiesAsync(string prefName, CancellationToken ct = default);
    Task<IReadOnlyList<Town>> GetTownsAsync(string prefName, string cityName, CancellationToken ct = default);
    Task<AddressParseResult?> ParseAsync(string address, AddressParseOptions? options = null, CancellationToken ct = default);
    /// <summary>
    /// <see cref="ParseAsync"/> と同じ解析を行い、解析できなかった場合はその理由も返す。
    /// </summary>
    Task<AddressParseOutcome> ParseWithReasonAsync(string address, AddressParseOptions? options = null, CancellationToken ct = default);
}
