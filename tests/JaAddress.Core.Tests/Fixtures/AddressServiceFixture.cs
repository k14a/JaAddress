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
                { "oaza_cho": "丸の内", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [139.7671, 35.6812] },
                { "oaza_cho": "大手町", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [139.7634, 35.6863] }
              ]
            }
            """);

        // 大阪府/大阪市.json
        File.WriteAllText(Path.Combine(dataDir, "ja", "大阪府", "大阪市.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "梅田", "chome": "一丁目", "chome_n": 1, "koaza": null, "rsdt": true, "point": [135.4964, 34.7024] }
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
