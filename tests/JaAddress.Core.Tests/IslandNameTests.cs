using JaAddress.Core;
using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Core.Tests;

/// <summary>
/// 慣習として町村名の前に書かれる島名（"東京都八丈島八丈町…"・"東京都三宅島三宅村…"）の読み飛ばしを検証する。
/// 辞書（アドレス・ベース・レジストリ）では島しょ部の町村は都の直下にあり、島名はない。
/// </summary>
public sealed class IslandNameTests : IDisposable {

    private readonly string _dataDir;
    private readonly IAddressService _sut;

    public IslandNameTests() {
        this._dataDir = Path.Combine(Path.GetTempPath(), $"JaAddressIsland_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(this._dataDir, "ja", "東京都"));

        File.WriteAllText(Path.Combine(this._dataDir, "ja.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                {
                  "code": 13000, "pref": "東京都", "point": [139.7, 35.7],
                  "cities": [
                    { "code": 13381, "city": "三宅村", "point": [139.5, 34.1] },
                    { "code": 13401, "city": "八丈町", "point": [139.8, 33.1] }
                  ]
                }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(this._dataDir, "ja", "東京都", "三宅村.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "坪田", "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [139.5, 34.1] }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(this._dataDir, "ja", "東京都", "八丈町.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "三根",   "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [139.8, 33.1] },
                { "oaza_cho": "大賀郷", "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [139.8, 33.1] }
              ]
            }
            """);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJaAddress(new JaAddressOptions { DataDirectory = this._dataDir });
        this._sut = services.BuildServiceProvider().GetRequiredService<IAddressService>();
    }

    public void Dispose() => Directory.Delete(this._dataDir, recursive: true);

    private static readonly AddressParseOptions SplitOptions = new() { SplitRemainder = true };

    [Theory]
    [InlineData("東京都八丈島八丈町三根1-2", "八丈町", "三根")]
    [InlineData("東京都三宅島三宅村坪田1-2", "三宅村", "坪田")]
    public async Task 島名のあとに町村名が続く場合_島名を読み飛ばす(string address, string expectedCity, string expectedTown) {
        var result = await this._sut.ParseAsync(address, SplitOptions);

        Assert.NotNull(result);
        Assert.Equal("東京都", result!.Prefecture.Name);
        Assert.Equal(expectedCity, result.City.Name);
        Assert.Equal(expectedTown, result.Town?.Name);
        Assert.Equal("1-2", result.Block);
        Assert.False(result.Corrected);
    }

    [Theory]
    [InlineData("東京都八丈島三根1-2", "八丈町", "三根")]
    [InlineData("東京都三宅島坪田1-2", "三宅村", "坪田")]
    public async Task 町村名がなく島名だけの場合_島名を町村名として補正する(string address, string expectedCity, string expectedTown) {
        var result = await this._sut.ParseAsync(address, SplitOptions);

        Assert.NotNull(result);
        Assert.Equal(expectedCity, result!.City.Name);
        Assert.Equal(expectedTown, result.Town?.Name);
        Assert.Equal("1-2", result.Block);
        Assert.True(result.Corrected);
    }

    [Fact]
    public async Task 島名のない住所はこれまでどおり解析する() {
        var result = await this._sut.ParseAsync("東京都八丈町大賀郷1-2", SplitOptions);

        Assert.NotNull(result);
        Assert.Equal("八丈町", result!.City.Name);
        Assert.Equal("大賀郷", result.Town?.Name);
        Assert.False(result.Corrected);
    }

    [Fact]
    public async Task BestEffort_前置きのある島名入りの住所を解析する() {
        var result = await this._sut.ParseAsync(
            "株式会社サンプル 東京都八丈島八丈町大賀郷1-2",
            new AddressParseOptions { SplitRemainder = true, BestEffort = true });

        Assert.NotNull(result);
        Assert.Equal("八丈町", result!.City.Name);
        Assert.Equal("大賀郷", result.Town?.Name);
        Assert.Equal("1-2", result.Block);
        Assert.Equal(9, result.Offset); // "株式会社サンプル " = 9文字
    }
}
