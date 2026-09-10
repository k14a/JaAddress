using JaAddress.Core.Services;

namespace JaAddress.Integration.Tests;

/// <summary>
/// 本番相当データ（<c>data/</c>）に対する住所パースの結合テスト。
/// <see cref="JaAddress.Core.Tests"/> の単体テストが合成フィクスチャを使うのに対し、
/// こちらは実データの表記ゆれ・件数・郡構造などを実際のファイルで検証する。
/// </summary>
public sealed class RealDataParseTests(RealDataFixture fixture) : IClassFixture<RealDataFixture> {
    private readonly RealDataFixture _fixture = fixture;

    private IAddressService Sut {
        get {
            Skip.IfNot(this._fixture.Available,
                "本番データ (data/) が見つかりません。`dotnet run --project src/JaAddress.DataBuilder -- --output ./data` で取得してください。");
            return this._fixture.AddressService!;
        }
    }

    // -------------------------------------------------------
    // マスタ一覧
    // -------------------------------------------------------
    [SkippableFact]
    public async Task GetPrefecturesAsync_47都道府県を返す() {
        var result = await this.Sut.GetPrefecturesAsync();

        Assert.Equal(47, result.Count);
        Assert.Contains(result, p => p.Name == "東京都");
        Assert.Contains(result, p => p.Name == "沖縄県");
    }

    [SkippableFact]
    public async Task GetCitiesAsync_政令市の区がwardとして読み込まれる() {
        var result = await this.Sut.GetCitiesAsync("北海道");

        var sapporoWards = result.Where(c => c.Ward is not null && c.Name.StartsWith("札幌市")).ToList();
        Assert.Contains(sapporoWards, c => c.Ward == "中央区");
        Assert.All(sapporoWards, c => Assert.Equal("札幌市" + c.Ward, c.DisplayName));
    }

    // -------------------------------------------------------
    // ParseAsync（丁目あり地区）
    // -------------------------------------------------------
    [SkippableFact]
    public async Task ParseAsync_都庁の住所_町字丁目番地まで分割される() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this.Sut.ParseAsync("東京都新宿区西新宿二丁目8-1", options);

        Assert.NotNull(result);
        Assert.Equal("東京都", result.Prefecture.Name);
        Assert.Equal("新宿区", result.City.Name);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("2丁目", result.Street);
        Assert.Equal("8-1", result.Block);
    }

    [SkippableFact]
    public async Task ParseAsync_全角番地_半角に正規化される() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this.Sut.ParseAsync("東京都新宿区西新宿２丁目８－１", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("2丁目", result.Street);
        Assert.Equal("8-1", result.Block);
    }

    // -------------------------------------------------------
    // ParseAsync（郡を含む市区町村・丁目なし地区）
    // -------------------------------------------------------
    [SkippableFact]
    public async Task ParseAsync_郡あり町_郡名込みで市区町村が特定される() {
        var result = await this.Sut.ParseAsync("奈良県吉野郡吉野町大字吉野山2498");

        Assert.NotNull(result);
        Assert.Equal("奈良県", result.Prefecture.Name);
        Assert.Equal("吉野郡吉野町", result.City.Name);
        Assert.Equal("吉野郡", result.City.County);
    }

    [SkippableFact]
    public async Task ParseAsync_郡省略_補正されてCorrectedがtrue() {
        var result = await this.Sut.ParseAsync("奈良県吉野町大字吉野山");

        Assert.NotNull(result);
        Assert.Equal("吉野郡吉野町", result.City.Name);
        Assert.True(result.Corrected);
    }

    // -------------------------------------------------------
    // ParseAsync（該当なし）
    // -------------------------------------------------------
    [SkippableFact]
    public async Task ParseAsync_実在しない住所_nullを返す() {
        var result = await this.Sut.ParseAsync("東京都新宿区存在しない町九丁目");

        // 町字は特定できないが都道府県・市区町村は取れる
        Assert.NotNull(result);
        Assert.Equal("新宿区", result.City.Name);
        Assert.Null(result.Town);
    }

    // -------------------------------------------------------
    // ParseAsync（勤務地テキストの表記ゆれ: 町名重複・都道府県の複数出現）
    // -------------------------------------------------------
    [SkippableTheory]
    [InlineData("⻄⿇布　りんどう 106-0031東京都港区西麻布西麻布１丁目１１番１３号 地図を見る",
        "港区", "西麻布", "1丁目", "11-13")]
    [InlineData("株式会社バディデータ 101-0047東京都千代田区内神田内神田２丁目１３番８号ＢＭビル２階 地図を見る",
        "千代田区", "内神田", "2丁目", "13-8")]
    public async Task ParseAsync_BestEffort_町名が連続重複_重複を読み飛ばして番地まで取れる(
        string address, string expectedCity, string expectedTown, string expectedStreet, string expectedBlock) {
        var options = new AddressParseOptions {
            SplitRemainder = true, NormalizeNumber = true, BestEffort = true, NormalizeOaza = true,
        };
        var result = await this.Sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("東京都", result.Prefecture.Name);
        Assert.Equal(expectedCity, result.City.Name);
        Assert.Equal(expectedTown, result.Town?.Name);
        Assert.Equal(expectedStreet, result.Street);
        Assert.Equal(expectedBlock, result.Block);
    }

    [SkippableFact]
    public async Task ParseAsync_BestEffort_会社名の括弧内に別の住所_後続の完全な住所を採用する() {
        var options = new AddressParseOptions {
            SplitRemainder = true, NormalizeNumber = true, BestEffort = true, NormalizeOaza = true,
        };
        var result = await this.Sut.ParseAsync(
            "ハーベスト株式会社(東京都中央区内の社員食堂) 東京都中央区日本橋本石町1-1-9 地図を見る", options);

        Assert.NotNull(result);
        Assert.Equal("中央区", result.City.Name);
        Assert.Equal("日本橋本石町", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("1-9", result.Block);
    }
}
