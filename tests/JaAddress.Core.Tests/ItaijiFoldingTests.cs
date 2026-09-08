using JaAddress.Core;
using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Core.Tests;

/// <summary>
/// 異体字（旧字体）畳み込み（<see cref="JaAddressOptions.FoldItaiji"/>）の挙動を検証する。
/// テスト用データを都度テンポラリディレクトリに生成する。
/// </summary>
public sealed class ItaijiFoldingTests {

    private static IServiceProvider BuildProvider(string dataDir, bool foldItaiji, IReadOnlyDictionary<char, char>? additional = null) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJaAddress(new JaAddressOptions {
            DataDirectory = dataDir,
            FoldItaiji = foldItaiji,
            AdditionalItaiji = additional,
        });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 辞書側に旧字体（須惠町・大字上須惠）を含むテストデータを生成する。
    /// 実データ（Geolonia）と同様、ja.json は新字、町字ファイルには旧字が混在する状況を再現する。
    /// </summary>
    private static string SetupTestData() {
        var dir = Path.Combine(Path.GetTempPath(), $"JaAddressItaiji_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "ja", "福岡県"));

        File.WriteAllText(Path.Combine(dir, "ja.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                {
                  "code": 40000,
                  "pref": "福岡県",
                  "point": [130.4, 33.6],
                  "cities": [
                    { "code": 40344, "county": "糟屋郡", "city": "須恵町", "point": [130.50, 33.58] }
                  ]
                }
              ]
            }
            """);

        // 町字ファイル名は新字（実データと同じ）。中身に旧字「大字上須惠」を混ぜる。
        File.WriteAllText(Path.Combine(dir, "ja", "福岡県", "糟屋郡須恵町.json"), """
            {
              "meta": { "updated": 20240101 },
              "data": [
                { "oaza_cho": "大字植木",   "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [130.50, 33.58] },
                { "oaza_cho": "大字上須惠", "chome": null, "chome_n": null, "koaza": null, "rsdt": false, "point": [130.51, 33.59] }
              ]
            }
            """);

        return dir;
    }

    [Fact]
    public async Task FoldItaiji無効時_旧字体の市区町村はパースできない() {
        var dir = SetupTestData();
        try {
            var svc = BuildProvider(dir, foldItaiji: false).GetRequiredService<IAddressService>();
            var result = await svc.ParseAsync("福岡県糟屋郡須惠町大字植木2000",
                new AddressParseOptions { SplitRemainder = true, NormalizeNumber = false });

            Assert.Null(result);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task FoldItaiji有効時_旧字体の市区町村をパースできる() {
        var dir = SetupTestData();
        try {
            var svc = BuildProvider(dir, foldItaiji: true).GetRequiredService<IAddressService>();
            var result = await svc.ParseAsync("福岡県糟屋郡須惠町大字植木2000",
                new AddressParseOptions { SplitRemainder = true, NormalizeNumber = false });

            Assert.NotNull(result);
            Assert.Equal("福岡県", result!.Prefecture.Name);
            Assert.Equal("糟屋郡須恵町", result.City.Name);
            Assert.Equal("大字植木", result.Town?.Name);
            Assert.Equal("2000", result.Block);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task FoldItaiji有効時_新字体の入力もこれまでどおりパースできる() {
        var dir = SetupTestData();
        try {
            var svc = BuildProvider(dir, foldItaiji: true).GetRequiredService<IAddressService>();
            var result = await svc.ParseAsync("福岡県糟屋郡須恵町大字植木2000",
                new AddressParseOptions { SplitRemainder = true, NormalizeNumber = false });

            Assert.NotNull(result);
            Assert.Equal("糟屋郡須恵町", result!.City.Name);
            Assert.Equal("大字植木", result.Town?.Name);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task FoldItaiji有効時_辞書側が旧字体の町字も新字体入力で一致する() {
        var dir = SetupTestData();
        try {
            var svc = BuildProvider(dir, foldItaiji: true).GetRequiredService<IAddressService>();
            // 辞書は「大字上須惠」(惠)、入力は「大字上須恵」(恵)
            var result = await svc.ParseAsync("福岡県糟屋郡須恵町大字上須恵5",
                new AddressParseOptions { SplitRemainder = true, NormalizeNumber = false });

            Assert.NotNull(result);
            Assert.Equal("大字上須惠", result!.Town?.Name); // 出力は辞書の正式表記(旧字)を保持
            Assert.Equal("5", result.Block);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AddressParseOptions_FoldItaiji_falseで明示的に無効化できる() {
        var dir = SetupTestData();
        try {
            var svc = BuildProvider(dir, foldItaiji: true).GetRequiredService<IAddressService>();
            var result = await svc.ParseAsync("福岡県糟屋郡須惠町大字植木2000",
                new AddressParseOptions { SplitRemainder = true, NormalizeNumber = false, FoldItaiji = false });

            Assert.Null(result);
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ItaijiFolder_既定マップが埋め込みリソースから読み込まれる() {
        var dir = SetupTestData();
        try {
            var folder = BuildProvider(dir, foldItaiji: true).GetRequiredService<IItaijiFolder>();

            Assert.True(folder.Enabled);
            Assert.True(folder.Map.Count > 0);
            Assert.Equal("恵", folder.Fold("惠"));
            Assert.Equal("株式会社サンプル", folder.Fold("株式会社サンプル")); // ヒットなしは素通し
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ItaijiFolder_FoldItaiji無効時は空マップで素通し() {
        var dir = SetupTestData();
        try {
            var folder = BuildProvider(dir, foldItaiji: false).GetRequiredService<IItaijiFolder>();

            Assert.False(folder.Enabled);
            Assert.Empty(folder.Map);
            Assert.Equal("惠", folder.Fold("惠"));
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ItaijiFolder_AdditionalItaijiが既定マップにマージされる() {
        var dir = SetupTestData();
        try {
            var folder = BuildProvider(dir, foldItaiji: true, additional: new Dictionary<char, char> { ['苅'] = '刈' })
                .GetRequiredService<IItaijiFolder>();

            Assert.Equal("刈", folder.Fold("苅"));
            Assert.Equal("恵", folder.Fold("惠")); // 既定分も残っている
        } finally {
            Directory.Delete(dir, recursive: true);
        }
    }
}
