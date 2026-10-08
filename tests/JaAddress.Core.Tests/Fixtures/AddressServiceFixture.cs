using JaAddress.Core;
using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Core.Tests.Fixtures;

public sealed class AddressServiceFixture : IDisposable {
    public string DataDirectory { get; }
    public IAddressService AddressService { get; }

    public AddressServiceFixture() {
        this.DataDirectory = Path.Combine(Path.GetTempPath(), $"JaAddressTest_{Guid.NewGuid():N}");
        SetupTestData(this.DataDirectory);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJaAddress(new JaAddressOptions { DataDirectory = this.DataDirectory });

        var provider = services.BuildServiceProvider();
        this.AddressService = provider.GetRequiredService<IAddressService>();
    }

    private static void SetupTestData(string dataDir) {
        Directory.CreateDirectory(Path.Combine(dataDir, "ja", "東京都"));
        Directory.CreateDirectory(Path.Combine(dataDir, "ja", "大阪府"));
        Directory.CreateDirectory(Path.Combine(dataDir, "ja", "奈良県"));

        // ja.json
        File.WriteAllText(Path.Combine(dataDir, "ja.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                {
                  "code": 13000,
                  "pref": "東京都",
                  "point": [139.6917, 35.6895],
                  "cities": [
                    { "code": 13104, "city": "新宿区",  "point": [139.7069, 35.6938] },
                    { "code": 13101, "city": "千代田区", "point": [139.7536, 35.6940] },
                    { "code": 13201, "city": "八王子市", "point": [139.3229, 35.6664] }
                  ]
                },
                {
                  "code": 27000,
                  "pref": "大阪府",
                  "point": [135.5022, 34.6937],
                  "cities": [
                    { "code": 27100, "city": "大阪市", "ward": "北区",  "point": [135.5100, 34.7024] },
                    { "code": 27101, "city": "大阪市", "ward": "中央区", "point": [135.5200, 34.6863] }
                  ]
                },
                {
                  "code": 29000,
                  "pref": "奈良県",
                  "point": [135.8048, 34.6851],
                  "cities": [
                    { "code": 29341, "county": "吉野郡", "city": "吉野町", "point": [135.8590, 34.4469] }
                  ]
                }
              ]
            }
            """);

        // 東京都/新宿区.json — oaza_cho/chome/chome_n/point 形式
        File.WriteAllText(Path.Combine(dataDir, "ja", "東京都", "新宿区.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "西新宿",   "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true,  "point": [139.6917, 35.6938] },
                { "oaza_cho": "西新宿",   "chome": "二丁目", "chome_n": 2, "koaza": null, "rsdt": true,  "point": [139.6920, 35.6940] },
                { "oaza_cho": "新宿",     "chome": null,     "chome_n": null, "koaza": null, "rsdt": false, "point": [139.7006, 35.6920] },
                { "oaza_cho": "歌舞伎町", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true,  "point": [139.7004, 35.6955] },
                { "oaza_cho": "歌舞伎町", "chome": "二丁目", "chome_n": 2, "koaza": null, "rsdt": true,  "point": [139.7005, 35.6956] }
              ]
            }
            """);

        // 東京都/千代田区.json
        File.WriteAllText(Path.Combine(dataDir, "ja", "東京都", "千代田区.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "丸の内", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true,  "point": [139.7671, 35.6812] },
                { "oaza_cho": "大手町", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true,  "point": [139.7634, 35.6863] },
                { "oaza_cho": "二番町", "chome": null,     "chome_n": null, "koaza": null, "rsdt": false, "point": [139.7361, 35.6862] }
              ]
            }
            """);

        // 大阪府/大阪市北区.json
        File.WriteAllText(Path.Combine(dataDir, "ja", "大阪府", "大阪市北区.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "梅田", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [135.4964, 34.7024] }
              ]
            }
            """);

        // 大阪府/大阪市中央区.json
        File.WriteAllText(Path.Combine(dataDir, "ja", "大阪府", "大阪市中央区.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "心斎橋筋", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [135.5016, 34.6730] }
              ]
            }
            """);

        // 奈良県/吉野郡吉野町.json — 丁目なし地区
        // 大字本城/本城/本城東 は北九州市八幡西区の町字構成を模したテスト用データ（大字付き町字と、それを接頭辞に持つ町字の識別用）
        // 大字六田の小字は伊達市保原町大泉等の構成を模したテスト用データ（「字」付き・「字」なしの小字、他の小字の前半に一致する小字）
        // 美園二条・北一条西・古町通五番町は札幌市・新潟市の漢数字の町字、平和通の小字は札幌市白石区の "十一丁目北"、
        // 村崎野の小字は岩手県北上市の地割（全角数字）、北崎町は丁目と小字の両方を持つ愛知県大府市の町字、来迎寺は地番の冠称（甲）の町字、
        // 蔵前町・蔵前町二丁は丁目のある町字と "N丁" で終わる町字が並ぶ堺市の町字を模したテスト用データ
        File.WriteAllText(Path.Combine(dataDir, "ja", "奈良県", "吉野郡吉野町.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "大字吉野山", "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [135.8590, 34.3653] },
                { "oaza_cho": "大字六田",   "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [135.8620, 34.4010] },
                { "oaza_cho": "大字六田",   "chome": null, "chome_n": null, "koaza": "字大地内", "rsdt": false, "point": null },
                { "oaza_cho": "大字六田",   "chome": null, "chome_n": null, "koaza": "字大地",   "rsdt": false, "point": null },
                { "oaza_cho": "大字六田",   "chome": null, "chome_n": null, "koaza": "字宮前",   "rsdt": false, "point": null },
                { "oaza_cho": "大字六田",   "chome": null, "chome_n": null, "koaza": "字東",     "rsdt": false, "point": null },
                { "oaza_cho": "大字六田",   "chome": null, "chome_n": null, "koaza": "中島",     "rsdt": false, "point": null },
                { "oaza_cho": "大字本城",   "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [135.8600, 34.3700] },
                { "oaza_cho": "本城",       "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [135.8601, 34.3701] },
                { "oaza_cho": "本城東",     "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [135.8602, 34.3702] },
                { "oaza_cho": "本城東",     "chome": "二丁目", "chome_n": 2, "koaza": null, "rsdt": true, "point": [135.8603, 34.3703] },
                { "oaza_cho": "美園二条",   "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": null },
                { "oaza_cho": "北一条西",   "chome": "二丁目", "chome_n": 2, "koaza": null, "rsdt": true, "point": null },
                { "oaza_cho": "古町通五番町", "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": null },
                { "oaza_cho": "平和通",     "chome": null, "chome_n": null, "koaza": "十一丁目北", "rsdt": false, "point": null },
                { "oaza_cho": "平和通",     "chome": null, "chome_n": null, "koaza": "十一丁目南", "rsdt": false, "point": null },
                { "oaza_cho": "村崎野",     "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": null },
                { "oaza_cho": "村崎野",     "chome": null, "chome_n": null, "koaza": "１地割",  "rsdt": false, "point": null },
                { "oaza_cho": "村崎野",     "chome": null, "chome_n": null, "koaza": "１４地割", "rsdt": false, "point": null },
                { "oaza_cho": "北崎町",     "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": null },
                { "oaza_cho": "北崎町",     "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": null },
                { "oaza_cho": "北崎町",     "chome": null, "chome_n": null, "koaza": "井田", "rsdt": false, "point": null },
                { "oaza_cho": "来迎寺",     "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": null },
                { "oaza_cho": "来迎寺",     "chome": null, "chome_n": null, "koaza": "八反田", "rsdt": false, "point": null },
                { "oaza_cho": "蔵前町",     "chome": "二丁目", "chome_n": 2, "koaza": null, "rsdt": true, "point": null },
                { "oaza_cho": "蔵前町二丁", "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": null }
              ]
            }
            """);
    }

    public void Dispose() {
        if (Directory.Exists(this.DataDirectory)) {
            Directory.Delete(this.DataDirectory, recursive: true);
        }
    }
}
