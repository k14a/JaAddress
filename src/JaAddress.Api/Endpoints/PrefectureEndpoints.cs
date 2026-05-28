using JaAddress.Core.Services;

namespace JaAddress.Api.Endpoints;

internal static class PrefectureEndpoints {
    public static void Map(WebApplication app) {
        var group = app.MapGroup("/prefectures").WithTags("住所マスター");

        group.MapGet("/", async (IAddressService svc, CancellationToken ct) => {
            var result = await svc.GetPrefecturesAsync(ct);
            return Results.Ok(result);
        })
        .WithSummary("都道府県一覧を取得する");

        group.MapGet("/{pref}/cities", async (
            string pref, IAddressService svc, CancellationToken ct) => {
            try {
                var result = await svc.GetCitiesAsync(
                    Uri.UnescapeDataString(pref), ct);
                return Results.Ok(result);
            } catch (ArgumentException) {
                return Results.NotFound($"都道府県が見つかりません: {pref}");
            }
        })
        .WithSummary("市区町村一覧を取得する");

        group.MapGet("/{pref}/cities/{city}/towns", async (
            string pref, string city, IAddressService svc, CancellationToken ct) => {
            var towns = await svc.GetTownsAsync(
                Uri.UnescapeDataString(pref),
                Uri.UnescapeDataString(city),
                ct);
            return Results.Ok(towns);
        })
        .WithSummary("町字一覧を取得する");
    }
}
