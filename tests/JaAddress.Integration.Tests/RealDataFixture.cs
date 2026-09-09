using JaAddress.Core;
using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Integration.Tests;

/// <summary>
/// リポジトリ直下の <c>data/</c>（DataBuilder が生成する本番相当データ、Git 管理外）を
/// そのまま読み込んで <see cref="IAddressService"/> を構築するフィクスチャ。
///
/// <para>
/// <c>data/</c> が存在しない環境（クリーンチェックアウト・CI 等）では
/// <see cref="Available"/> が false になり、各テストは <c>Skip.IfNot</c> でスキップされる。
/// データを用意するには <c>dotnet run --project src/JaAddress.DataBuilder -- --output ./data</c> を実行する。
/// </para>
/// </summary>
public sealed class RealDataFixture {
    public bool Available { get; }
    public string? DataDirectory { get; }
    public IAddressService? AddressService { get; }

    public RealDataFixture() {
        this.DataDirectory = LocateDataDirectory();
        if (this.DataDirectory is null) {
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJaAddress(new JaAddressOptions { DataDirectory = this.DataDirectory });

        var provider = services.BuildServiceProvider();
        this.AddressService = provider.GetRequiredService<IAddressService>();
        this.Available = true;
    }

    /// <summary>
    /// テスト実行ディレクトリから親をたどり、<c>data/ja.json</c> を含む <c>data/</c> を探す。
    /// 見つからなければ null。
    /// </summary>
    private static string? LocateDataDirectory() {
        // 環境変数による明示指定を優先（CI 等でデータ配置が固定の場合）。
        var fromEnv = Environment.GetEnvironmentVariable("JAADDRESS_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(Path.Combine(fromEnv, "ja.json"))) {
            return fromEnv;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "ja.json"))) {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
