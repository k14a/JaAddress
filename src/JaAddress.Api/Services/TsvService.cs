using System.Text;
using JaAddress.Api.Models;

namespace JaAddress.Api.Services;

internal sealed class TsvService {
    private static readonly string[] Headers =
        ["address", "prefecture", "city", "county", "town", "street", "block", "remainder", "corrected", "offset"];

    public static string BuildTemplate() =>
        string.Join('\t', Headers) + "\n";

    public static (IReadOnlyList<string> Addresses, string? Error) ParseUpload(
        Stream stream, int maxRows) {

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var addresses = new List<string>();

        // 1行目はヘッダ
        var header = reader.ReadLine();
        if (header is null) {
            return ([], "ファイルが空です。");
        }

        while (!reader.EndOfStream) {
            if (addresses.Count >= maxRows) {
                return ([], $"TSVの行数が上限（{maxRows}件）を超えています。");
            }

            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            var columns = line.Split('\t');
            var address = columns[0].Trim();
            if (!string.IsNullOrEmpty(address)) {
                addresses.Add(address);
            }
        }

        return (addresses, null);
    }

    public static string BuildResult(IReadOnlyList<ParseResultDto> results) {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('\t', Headers));
        foreach (var r in results) {
            sb.AppendLine(string.Join('\t', [
                r.Input,
                r.Prefecture ?? string.Empty,
                r.City       ?? string.Empty,
                r.County     ?? string.Empty,
                r.Town       ?? string.Empty,
                r.Street     ?? string.Empty,
                r.Block      ?? string.Empty,
                r.Remainder  ?? string.Empty,
                r.Corrected ? "true" : string.Empty,
                r.Offset > 0 ? r.Offset.ToString() : string.Empty,
            ]));
        }
        return sb.ToString();
    }
}
