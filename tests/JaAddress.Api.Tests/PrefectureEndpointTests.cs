using System.Net;
using System.Text.Json;

namespace JaAddress.Api.Tests;

public sealed class PrefectureEndpointTests(ApiTestFixture fixture)
    : IClassFixture<ApiTestFixture> {

    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task GetPrefectures_正常系_200と都道府県リストを返す() {
        var response = await _client.GetAsync("/prefectures");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(1, arr.GetArrayLength());
        Assert.Equal("東京都", arr[0].GetProperty("name").GetString());
        Assert.Equal(13000, arr[0].GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task GetCities_正常系_200と市区町村リストを返す() {
        var pref = Uri.EscapeDataString("東京都");
        var response = await _client.GetAsync($"/prefectures/{pref}/cities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(1, arr.GetArrayLength());
        Assert.Equal("新宿区", arr[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetCities_存在しない都道府県_404を返す() {
        var pref = Uri.EscapeDataString("存在しない県");
        var response = await _client.GetAsync($"/prefectures/{pref}/cities");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTowns_正常系_200と町字リストを返す() {
        var pref = Uri.EscapeDataString("東京都");
        var city = Uri.EscapeDataString("新宿区");
        var response = await _client.GetAsync($"/prefectures/{pref}/cities/{city}/towns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(1, arr.GetArrayLength());
        Assert.Equal("西新宿", arr[0].GetProperty("name").GetString());
        Assert.Equal("新宿区", arr[0].GetProperty("cityName").GetString());
    }

    [Fact]
    public async Task GetTowns_存在しない市区町村_200と空リストを返す() {
        var pref = Uri.EscapeDataString("東京都");
        var city = Uri.EscapeDataString("存在しない区");
        var response = await _client.GetAsync($"/prefectures/{pref}/cities/{city}/towns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }
}
