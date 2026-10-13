using System.Formats.Tar;
using System.IO.Compression;

namespace JaAddress.DataBuilder.Processors;

/// <summary>
/// 構築したデータディレクトリを 1 つのアーカイブファイルにまとめる。
/// 形式はアーカイブのファイル名の拡張子で決める（tar.gz: .tar.gz / .tgz、zip: .zip）。
/// </summary>
internal static class DataArchiver {
    /// <summary>
    /// 対応しているアーカイブの拡張子（大文字小文字は区別しない）。
    /// </summary>
    internal static readonly string[] SupportedExtensions = [".tar.gz", ".tgz", ".zip"];

    /// <summary>
    /// 指定したパスの拡張子がアーカイブとして対応しているかを返す。
    /// </summary>
    internal static bool IsSupported(string archivePath) =>
        SupportedExtensions.Any(ext => archivePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <paramref name="sourceDir"/> の中身をアーカイブにまとめて <paramref name="archivePath"/> に保存する。
    /// アーカイブ内のパスは <paramref name="sourceDir"/> からの相対パス（例: <c>ja.json</c>、<c>ja/東京都/新宿区.json</c>）。
    /// 書き込み途中のファイルが残らないよう、同じディレクトリの一時ファイルに書いてから置き換える。
    /// </summary>
    internal static async Task CreateAsync(string sourceDir, string archivePath, CancellationToken ct = default) {
        if (!IsSupported(archivePath)) {
            throw new ArgumentException(
                $"対応していないアーカイブ形式です（{string.Join(" / ", SupportedExtensions)} のみ）: {archivePath}",
                nameof(archivePath));
        }

        var fullPath = Path.GetFullPath(archivePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var tempPath = Path.Combine(dir, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try {
            if (fullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                // ファイル名に日本語を含むため、エントリ名は UTF-8（UTF-8 フラグ付き）で書き込まれる
                await ZipFile.CreateFromDirectoryAsync(sourceDir, tempPath, CompressionLevel.SmallestSize, includeBaseDirectory: false, ct);
            } else {
                await using var file = File.Create(tempPath);
                await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
                await TarFile.CreateFromDirectoryAsync(sourceDir, gzip, includeBaseDirectory: false, ct);
            }
            File.Move(tempPath, fullPath, overwrite: true);
        } finally {
            if (File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        }
    }
}
