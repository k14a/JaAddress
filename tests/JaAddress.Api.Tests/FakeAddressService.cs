using JaAddress.Core.Models;
using JaAddress.Core.Services;

namespace JaAddress.Api.Tests;

internal sealed class FakeAddressService : IAddressService {
    private static readonly Prefecture FakePref = new() {
        Code = 13000, Name = "東京都", Latitude = 35.6895m, Longitude = 139.6917m,
    };
    private static readonly City FakeCity = new() {
        Code = 13104, PrefectureName = "東京都", Name = "新宿区",
        Latitude = 35.6938m, Longitude = 139.7034m,
    };
    private static readonly Town FakeTown = new() {
        PrefectureName = "東京都", CityName = "新宿区", Name = "西新宿",
        Latitude = 35.6896m, Longitude = 139.6922m,
    };

    public Task<IReadOnlyList<Prefecture>> GetPrefecturesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Prefecture>>([FakePref]);

    public Task<IReadOnlyList<City>> GetCitiesAsync(string prefName, CancellationToken ct = default) {
        if (prefName != "東京都")
            throw new ArgumentException($"都道府県が見つかりません: {prefName}");
        return Task.FromResult<IReadOnlyList<City>>([FakeCity]);
    }

    public Task<IReadOnlyList<Town>> GetTownsAsync(string prefName, string cityName, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Town>>(
            cityName == "新宿区" ? (IReadOnlyList<Town>)[FakeTown] : []);

    public Task<AddressParseResult?> ParseAsync(
        string address, AddressParseOptions? options = null, CancellationToken ct = default) {
        if (!address.StartsWith("東京都新宿区"))
            return Task.FromResult<AddressParseResult?>(null);

        return Task.FromResult<AddressParseResult?>(new AddressParseResult {
            Prefecture = FakePref,
            City       = FakeCity,
            Town       = FakeTown,
            Street     = "1丁目",
            Block      = "2-3",
            Remainder  = string.Empty,
        });
    }
}
