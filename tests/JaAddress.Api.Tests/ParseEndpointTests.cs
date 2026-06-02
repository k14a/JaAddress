using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace JaAddress.Api.Tests;

public sealed class ParseEndpointTests(ApiTestFixture fixture)
    : IClassFixture<ApiTestFixture> {

    private readonly HttpClient _client = fixture.CreateClient();

    // -------------------------------------------------------
    // GET /parse
    // -------------------------------------------------------
    [Fact]
    public async Task GetParse_正常系_200とパース結果を返す() {
        var q = Uri.EscapeDataString("東京都新宿区西新宿1丁目2-3");
        var response = await _client.GetAsync($"/parse?q={q}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("東京都", root.GetProperty("prefecture").GetString());
        Assert.Equal("新宿区", root.GetProperty("city").GetString());
        Assert.Equal("西新宿", root.GetProperty("town").GetString());
        Assert.Equal("1丁目", root.GetProperty("street").GetString());
        Assert.Equal("2-3", root.GetProperty("block").GetString());
    }

    [Fact]
    public async Task GetParse_特定できない住所_404とsuccessFalseを返す() {
        var q = Uri.EscapeDataString("存在しない県どこか市");
        var response = await _client.GetAsync($"/parse?q={q}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task GetParse_入力文字列がinputフィールドに含まれる() {
        var address = "東京都新宿区西新宿1丁目2-3";
        var q = Uri.EscapeDataString(address);
        var response = await _client.GetAsync($"/parse?q={q}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(address, doc.RootElement.GetProperty("input").GetString());
    }

    // -------------------------------------------------------
    // POST /parse
    // -------------------------------------------------------
    [Fact]
    public async Task PostParse_正常系_200とResultsリストを返す() {
        var request = new {
            addresses = new[] { "東京都新宿区西新宿1丁目2-3", "存在しない県どこか市" },
        };
        var response = await _client.PostAsJsonAsync("/parse", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("count").GetInt32());
        var results = root.GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());
        Assert.True(results[0].GetProperty("success").GetBoolean());
        Assert.False(results[1].GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task PostParse_件数超過_400を返す() {
        var addresses = Enumerable.Range(0, 21).Select(i => $"東京都新宿区{i}").ToArray();
        var response = await _client.PostAsJsonAsync("/parse", new { addresses });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------
    // GET /parse/tsv/template
    // -------------------------------------------------------
    [Fact]
    public async Task GetParseTsvTemplate_200とTSVヘッダを返す() {
        var response = await _client.GetAsync("/parse/tsv/template");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/tab-separated-values",
            response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        var headerLine = content.Split('\n')[0];
        Assert.Equal(
            "address\tprefecture\tcity\tcounty\ttown\tstreet\tblock\tremainder\tcorrected\toffset",
            headerLine);
    }

    // -------------------------------------------------------
    // POST /parse/tsv
    // -------------------------------------------------------
    [Fact]
    public async Task PostParseTsv_正常系_200と結果TSVを返す() {
        var tsvContent = "address\n東京都新宿区西新宿1丁目2-3\n";
        using var form = new MultipartFormDataContent();
        form.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes(tsvContent)) {
                Headers = { ContentType = new("text/tab-separated-values") },
            },
            "file", "input.tsv");

        var response = await _client.PostAsync("/parse/tsv", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/tab-separated-values",
            response.Content.Headers.ContentType?.MediaType);
        var result = await response.Content.ReadAsStringAsync();
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2);
        // ヘッダ行に正しい列が含まれる
        Assert.Contains("prefecture", lines[0]);
        Assert.Contains("county", lines[0]);
        // データ行に東京都・新宿区が含まれる
        Assert.Contains("東京都", lines[1]);
        Assert.Contains("新宿区", lines[1]);
    }

    [Fact]
    public async Task PostParseTsv_空ファイル_400を返す() {
        using var form = new MultipartFormDataContent();
        form.Add(
            new ByteArrayContent([]) {
                Headers = { ContentType = new("text/tab-separated-values") },
            },
            "file", "empty.tsv");

        var response = await _client.PostAsync("/parse/tsv", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
