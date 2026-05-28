using JaAddress.Api.Models;
using JaAddress.Api.Options;
using JaAddress.Api.Services;
using JaAddress.Core.Services;
using Microsoft.Extensions.Options;

namespace JaAddress.Api.Endpoints;

internal static class ParseEndpoints {
    public static void Map(WebApplication app) {
        var group = app.MapGroup("/parse").WithTags("住所パース");

        // GET /parse?q=...
        group.MapGet("/", async (
            string q,
            IAddressService svc,
            CancellationToken ct) => {

            var options = new AddressParseOptions { SplitRemainder = true };
            var result  = await svc.ParseAsync(q, options, ct);
            var dto     = ParseDtoMapper.ToDto(q, result);
            return dto.Success ? Results.Ok(dto) : Results.NotFound(dto);
        })
        .WithSummary("住所を1件パースする");

        // POST /parse (JSON)
        group.MapPost("/", async (
            ParseRequest request,
            IAddressService svc,
            IOptions<ApiOptions> apiOptions,
            CancellationToken ct) => {

            var max = apiOptions.Value.MaxJsonBatchSize;
            if (request.Addresses.Count > max)
                return Results.BadRequest($"一度に処理できるのは {max} 件までです。");

            var parseOptions = new AddressParseOptions { SplitRemainder = true };
            var results = new List<ParseResultDto>(request.Addresses.Count);
            foreach (var address in request.Addresses) {
                var result = await svc.ParseAsync(address, parseOptions, ct);
                results.Add(ParseDtoMapper.ToDto(address, result));
            }

            return Results.Ok(new ParseResponse {
                Results = results,
                Count   = results.Count,
            });
        })
        .WithSummary("住所を複数件パースする（JSON、最大20件）");

        // GET /parse/tsv/template
        group.MapGet("/tsv/template", () => {
            var tsv = TsvService.BuildTemplate();
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(tsv),
                contentType: "text/tab-separated-values",
                fileDownloadName: "template.tsv");
        })
        .WithSummary("TSVテンプレートをダウンロードする");

        // POST /parse/tsv
        group.MapPost("/tsv", async (
            IFormFile file,
            IAddressService svc,
            IOptions<ApiOptions> apiOptions,
            CancellationToken ct) => {

            var max = apiOptions.Value.MaxTsvRows;
            await using var stream = file.OpenReadStream();
            var (addresses, error) = TsvService.ParseUpload(stream, max);
            if (error is not null)
                return Results.BadRequest(error);

            var parseOptions = new AddressParseOptions { SplitRemainder = true };
            var results = new List<ParseResultDto>(addresses.Count);
            foreach (var address in addresses) {
                var result = await svc.ParseAsync(address, parseOptions, ct);
                results.Add(ParseDtoMapper.ToDto(address, result));
            }

            var tsv = TsvService.BuildResult(results);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(tsv),
                contentType: "text/tab-separated-values",
                fileDownloadName: "result.tsv");
        })
        .WithSummary("TSVファイルで住所を一括パースする");
    }
}
