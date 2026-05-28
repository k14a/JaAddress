using JaAddress.Core;
using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Core.Tests.Fixtures;

/// <summary>
/// テスト用のデータディレクトリを用意してDIコンテナを構築するフィクスチャ。
/// xUnitのIClassFixtureで共有する。
/// </summary>
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

    /// <summary>テスト用の最小限JSONを生成する。</summary>
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
                    { "code": 13104, "city": "新宿区", "point": [139.7069, 35.6938] },
                    { "code": 13101, "city": "千代田区", "point": [139.7536, 35.6940] },
                    { "code": 13201, "city": "八王子市", "point": [139.3229, 35.6664] }
                  ]
                },
                {
                  "code": 27000,
                  "pref": "大阪府",
                  "point": [135.5022, 34.6937],
                  "cities": [
                    { "code": 27100, "city": "大阪市", "ward": "北区", "point": [135.5100, 34.7024] },
                    { "code": 27101, "city": "大阪市", "ward": "中央区", "point": [135.5200, 34.6863] }
                  ]
                }
              ]
            }
            """);

        // 東京都/新宿区.json
        File.WriteAllText(Path.Combine(dataDir, "ja", "東京都", "新宿区.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "town": "西新宿", "koaza": null, "lat": 35.6938, "lng": 139.6917 },
                { "town": "新宿",   "koaza": null, "lat": 35.6920, "lng": 139.7006 },
                { "town": "歌舞伎町", "koaza": null, "lat": 35.6955, "lng": 139.7004 }
              ]
            }
            """);

        // 東京都/千代田区.json
        File.WriteAllText(Path.Combine(dataDir, "ja", "東京都", "千代田区.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "town": "丸の内", "koaza": null, "lat": 35.6812, "lng": 139.7671 },
                { "town": "大手町", "koaza": null, "lat": 35.6863, "lng": 139.7634 }
              ]
            }
            """);

        // 大阪府/大阪市.json（wardあり）
        File.WriteAllText(Path.Combine(dataDir, "ja", "大阪府", "大阪市.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "town": "梅田", "koaza": null, "lat": 34.7024, "lng": 135.4964 }
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
