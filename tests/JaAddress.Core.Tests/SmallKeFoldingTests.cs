using JaAddress.Core;
using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Core.Tests;

/// <summary>
/// 小書きのヶ・ヵ・ゖ をケに畳み込む表記揺れの吸収（常に有効）を検証する。
/// 実データ（Geolonia）は名前ごとにヶ・ケが混在し、同じ町字がヶ・ケ両方の表記で重複していることもある。
/// </summary>
public sealed class SmallKeFoldingTests {

    private static IAddressService BuildService(string dataDir, bool foldItaiji = false) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJaAddress(new JaAddressOptions { DataDirectory = dataDir, FoldItaiji = foldItaiji });
        return services.BuildServiceProvider().GetRequiredService<IAddressService>();
    }

    private static readonly AddressParseOptions SplitOptions = new() { SplitRemainder = true };

    /// <summary>
    /// 古ケ場（さいたま市岩槻区）は辞書がケ、茅ヶ崎市は市区町村名がヶ、
    /// 野向町牛ヶ谷・野向町牛ケ谷（勝山市）は同じ町字が両方の表記で重複し、小字がそれぞれに分かれている実データを模したテスト用データ。
    /// </summary>
    private static string SetupTestData() {
        var dir = Path.Combine(Path.GetTempPath(), $"JaAddressSmallKe_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "ja", "埼玉県"));
        Directory.CreateDirectory(Path.Combine(dir, "ja", "神奈川県"));
        Directory.CreateDirectory(Path.Combine(dir, "ja", "福井県"));

        File.WriteAllText(Path.Combine(dir, "ja.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                {
                  "code": 11000, "pref": "埼玉県", "point": [139.6, 35.8],
                  "cities": [ { "code": 11109, "city": "さいたま市", "ward": "岩槻区", "point": [139.7, 35.9] } ]
                },
                {
                  "code": 14000, "pref": "神奈川県", "point": [139.6, 35.4],
                  "cities": [ { "code": 14207, "city": "茅ヶ崎市", "point": [139.4, 35.3] } ]
                },
                {
                  "code": 18000, "pref": "福井県", "point": [136.2, 36.0],
                  "cities": [ { "code": 18206, "city": "勝山市", "point": [136.5, 36.0] } ]
                }
              ]
            }
            """);

        File.WriteAllText(Path.Combine(dir, "ja", "埼玉県", "さいたま市岩槻区.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "古ケ場", "chome": null,     "chome_n": null, "koaza": null, "rsdt": false, "point": [139.7, 35.9] },
                { "oaza_cho": "古ケ場", "chome": "一丁目", "chome_n": 1,    "koaza": null, "rsdt": true,  "point": [139.7, 35.9] },
                { "oaza_cho": "古ケ場", "chome": "二丁目", "chome_n": 2,    "koaza": null, "rsdt": true,  "point": [139.7, 35.9] }
              ]
            }
            """);

        File.WriteAllText(Path.Combine(dir, "ja", "神奈川県", "茅ヶ崎市.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "東海岸北", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [139.4, 35.3] }
              ]
            }
            """);

        File.WriteAllText(Path.Combine(dir, "ja", "福井県", "勝山市.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "野向町牛ヶ谷", "chome": null, "chome_n": null, "koaza": "大坪", "rsdt": false, "point": [136.5, 36.0] },
                { "oaza_cho": "野向町牛ケ谷", "chome": null, "chome_n": null, "koaza": "中島", "rsdt": false, "point": [136.5, 36.0] }
              ]
            }
            """);

        return dir;
    }

    [Theory]
    [InlineData("埼玉県さいたま市岩槻区古ヶ場1丁目3-7")]
    [InlineData("埼玉県さいたま市岩槻区古ケ場1丁目3-7")]
    [InlineData("埼玉県さいたま市岩槻区古ヵ場1丁目3-7")]
    [InlineData("埼玉県さいたま市岩槻区古ゖ場1丁目3-7")]
    [InlineData("埼玉県さいたま市岩槻区古ヶ場1-3-7")]
    public async Task 町字のヶとケとヵとゖの違いを同一視する(string address) {
        var dir = SetupTestData();
        try {
            var result = await BuildService(dir).ParseAsync(address, SplitOptions);

            Assert.NotNull(result);
            Assert.Equal("古ケ場", result!.Town?.Name); // 出力は辞書の表記
            Assert.Equal("1丁目", result.Street);
            Assert.Equal("3-7", result.Block);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("神奈川県茅ヶ崎市東海岸北1丁目2-3")]
    [InlineData("神奈川県茅ケ崎市東海岸北1丁目2-3")]
    public async Task 市区町村名のヶとケの違いを同一視する(string address) {
        var dir = SetupTestData();
        try {
            var result = await BuildService(dir).ParseAsync(address, SplitOptions);

            Assert.NotNull(result);
            Assert.Equal("茅ヶ崎市", result!.City.Name);
            Assert.Equal("東海岸北", result.Town?.Name);
            Assert.Equal("2-3", result.Block);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task 異体字の畳み込みが有効でもヶとケの違いを同一視する() {
        var dir = SetupTestData();
        try {
            var result = await BuildService(dir, foldItaiji: true).ParseAsync("埼玉県さいたま市岩槻区古ヶ場1丁目3-7", SplitOptions);

            Assert.NotNull(result);
            Assert.Equal("古ケ場", result!.Town?.Name);
            Assert.Equal("3-7", result.Block);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("福井県勝山市野向町牛ヶ谷大坪10", "大坪")]
    [InlineData("福井県勝山市野向町牛ヶ谷中島10", "中島")]
    [InlineData("福井県勝山市野向町牛ケ谷大坪10", "大坪")]
    [InlineData("福井県勝山市野向町牛ケ谷中島10", "中島")]
    public async Task 辞書にヶとケ両方の表記で重複した町字は_同じ町字に畳み込み両方の小字を読む(string address, string koaza) {
        var dir = SetupTestData();
        try {
            var service = BuildService(dir);
            var result = await service.ParseAsync(address, SplitOptions);
            // 入力のヶ・ケによらず同じ町字になる（別々の表記で書かれた同じ住所の解析結果がそろう）
            var other = await service.ParseAsync(address.Replace('ヶ', 'ケ'), SplitOptions);

            Assert.NotNull(result);
            Assert.Equal(other!.Town?.Name, result!.Town?.Name);
            Assert.Equal(koaza, result.Koaza);
            Assert.Equal("10", result.Block);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }
}
