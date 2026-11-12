using System.Formats.Tar;
using System.IO.Compression;
using JaAddress.DataBuilder.Processors;

namespace JaAddress.DataBuilder.Tests;

public sealed class DataArchiverTests : IDisposable {
    // Path.GetTempPath() は macOS では /var/folders/...（/var は /private/var へのシンボリックリンク）を返し、
    // 展開先のパスにシンボリックリンクを含むと TarFile.ExtractToDirectoryAsync が ArgumentOutOfRangeException で
    // 失敗するため、テストの出力ディレクトリの下を作業ディレクトリにする
    private readonly string _workDir = Path.Combine(AppContext.BaseDirectory, $"DataArchiverTests.{Guid.NewGuid():N}");
    private readonly string _sourceDir;

    // DataBuilder の出力と同じ構成（ja.json と ja/{都道府県名}/{市区町村名}.json）
    private static readonly Dictionary<string, string> SourceFiles = new() {
        ["ja.json"] = """{"data":[{"pref":"東京都"}]}""",
        [Path.Combine("ja", "東京都", "新宿区.json")] = """{"data":[{"machiaza":"西新宿"}]}""",
        [Path.Combine("ja", "北海道", "札幌市中央区.json")] = """{"data":[{"machiaza":"北一条西"}]}""",
    };

    public DataArchiverTests() {
        this._sourceDir = Path.Combine(this._workDir, "data");
        foreach (var (relativePath, content) in SourceFiles) {
            var path = Path.Combine(this._sourceDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }

    public void Dispose() {
        if (Directory.Exists(this._workDir)) {
            Directory.Delete(this._workDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("ja-address-data.tar.gz")]
    [InlineData("ja-address-data.tgz")]
    [InlineData("ja-address-data.zip")]
    [InlineData("JA-ADDRESS-DATA.TAR.GZ")]
    public async Task CreateAsync_RoundTripsAllFilesWithRelativePaths(string archiveName) {
        var archivePath = Path.Combine(this._workDir, "out", archiveName);

        await DataArchiver.CreateAsync(this._sourceDir, archivePath);

        var extractDir = Path.Combine(this._workDir, "extract");
        Directory.CreateDirectory(extractDir);
        if (archiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
            await ZipFile.ExtractToDirectoryAsync(archivePath, extractDir);
        } else {
            await using var file = File.OpenRead(archivePath);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzip, extractDir, overwriteFiles: false);
        }

        // アーカイブ内のパスは data ディレクトリからの相対パス（data/ 自体は含まない）。日本語のファイル名もそのまま戻る
        var extracted = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(extractDir, f))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(SourceFiles.Keys.Order(StringComparer.Ordinal), extracted);
        foreach (var (relativePath, content) in SourceFiles) {
            Assert.Equal(content, File.ReadAllText(Path.Combine(extractDir, relativePath)));
        }
        // 一時ファイルを残さない
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(archivePath)!));
    }

    [Fact]
    public async Task CreateAsync_OverwritesExistingArchive() {
        var archivePath = Path.Combine(this._workDir, "ja-address-data.zip");
        File.WriteAllText(archivePath, "old");

        await DataArchiver.CreateAsync(this._sourceDir, archivePath);

        using var zip = ZipFile.OpenRead(archivePath);
        Assert.Equal(SourceFiles.Count, zip.Entries.Count(e => !string.IsNullOrEmpty(e.Name)));
    }

    [Theory]
    [InlineData("ja-address-data.tar")]
    [InlineData("ja-address-data.gz")]
    [InlineData("ja-address-data.7z")]
    public async Task CreateAsync_UnsupportedExtension_Throws(string archiveName) {
        var archivePath = Path.Combine(this._workDir, archiveName);

        Assert.False(DataArchiver.IsSupported(archivePath));
        await Assert.ThrowsAsync<ArgumentException>(() => DataArchiver.CreateAsync(this._sourceDir, archivePath));
        Assert.False(File.Exists(archivePath));
    }
}
