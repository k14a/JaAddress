using JaAddress.Core.Services;
using JaAddress.Core.Tests.Fixtures;

namespace JaAddress.Core.Tests;

public sealed class AddressServiceTests(AddressServiceFixture fixture)
    : IClassFixture<AddressServiceFixture> {

    private readonly IAddressService _sut = fixture.AddressService;

    // -------------------------------------------------------
    // GetPrefecturesAsync
    // -------------------------------------------------------
    [Fact]
    public async Task GetPrefecturesAsync_正常系_都道府県一覧を返す() {
        var result = await this._sut.GetPrefecturesAsync();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, p => p.Name == "東京都");
        Assert.Contains(result, p => p.Name == "大阪府");
        Assert.Contains(result, p => p.Name == "奈良県");
    }

    [Fact]
    public async Task GetPrefecturesAsync_座標が正しく変換される() {
        var result = await this._sut.GetPrefecturesAsync();
        var tokyo = result.Single(p => p.Name == "東京都");

        Assert.Equal(139.6917m, tokyo.Longitude);
        Assert.Equal(35.6895m, tokyo.Latitude);
    }

    // -------------------------------------------------------
    // GetCitiesAsync
    // -------------------------------------------------------
    [Fact]
    public async Task GetCitiesAsync_正常系_市区町村一覧を返す() {
        var result = await this._sut.GetCitiesAsync("東京都");

        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Name == "新宿区");
        Assert.Contains(result, c => c.Name == "千代田区");
        Assert.Contains(result, c => c.Name == "八王子市");
    }

    [Fact]
    public async Task GetCitiesAsync_Wardあり_DisplayNameが結合される() {
        var result = await this._sut.GetCitiesAsync("大阪府");

        var city = result.First(c => c.Ward == "北区");
        Assert.Equal("大阪市北区", city.DisplayName);
    }

    [Fact]
    public async Task GetCitiesAsync_存在しない都道府県_例外をスローする() {
        await Assert.ThrowsAsync<ArgumentException>(
            () => this._sut.GetCitiesAsync("存在しない県"));
    }

    // -------------------------------------------------------
    // GetTownsAsync
    // -------------------------------------------------------
    [Fact]
    public async Task GetTownsAsync_正常系_町字一覧を返す() {
        var result = await this._sut.GetTownsAsync("東京都", "新宿区");

        Assert.Equal(3, result.Count);
        Assert.Contains(result, t => t.Name == "西新宿");
        Assert.Contains(result, t => t.Name == "歌舞伎町");
    }

    [Fact]
    public async Task GetTownsAsync_存在しない市区町村_空リストを返す() {
        var result = await this._sut.GetTownsAsync("東京都", "存在しない区");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTownsAsync_政令市名指定_全区の町字を集約して返す() {
        var result = await this._sut.GetTownsAsync("大阪府", "大阪市");

        Assert.NotEmpty(result);
        Assert.Contains(result, t => t.Name == "梅田");
        Assert.Contains(result, t => t.Name == "心斎橋筋");
    }

    [Fact]
    public async Task GetTownsAsync_郡名指定_全町村の町字を集約して返す() {
        var result = await this._sut.GetTownsAsync("奈良県", "吉野郡");

        Assert.NotEmpty(result);
        Assert.Contains(result, t => t.Name == "大字吉野山");
        Assert.Contains(result, t => t.Name == "大字六田");
    }

    // -------------------------------------------------------
    // ParseAsync - SplitRemainder=false（デフォルト）
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_都道府県と市区町村を特定してRemainderを返す() {
        var result = await this._sut.ParseAsync("東京都新宿区西新宿1丁目2-3");

        Assert.NotNull(result);
        Assert.Equal("東京都", result.Prefecture.Name);
        Assert.Equal("新宿区", result.City.Name);
        Assert.Equal("西新宿1丁目2-3", result.Remainder);
        Assert.Null(result.Town);
    }

    [Fact]
    public async Task ParseAsync_存在しない住所_nullを返す() {
        var result = await this._sut.ParseAsync("存在しない県どこか市");

        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_都道府県のみ_nullを返す() {
        var result = await this._sut.ParseAsync("東京都");

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // ParseAsync - SplitRemainder=true
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_SplitRemainder_町字Street_Blockを分割する() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都新宿区西新宿1丁目2-3", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
    }

    [Fact]
    public async Task ParseAsync_SplitRemainder_町字のみ_StreetBlockがnull() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都新宿区西新宿", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Null(result.Street);
        Assert.Null(result.Block);
    }

    // -------------------------------------------------------
    // ParseAsync - NormalizeNumber
    // -------------------------------------------------------
    [Theory]
    [InlineData("東京都新宿区西新宿１丁目２－３")]  // 全角数字 + 全角ハイフン U+FF0D
    [InlineData("東京都新宿区西新宿１丁目２−３")]   // 全角数字 + マイナス記号 U+2212
    [InlineData("東京都新宿区西新宿１丁目２ー３")]   // 全角数字 + 長音符 U+30FC
    public async Task ParseAsync_全角数字_正規化して分割する(string address) {
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeNumber = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
    }

    [Fact]
    public async Task ParseAsync_SplitRemainder_丁目省略形_半角数字丁目で返す() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都新宿区西新宿1-2-3", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
    }

    [Fact]
    public async Task ParseAsync_SplitRemainder_漢字丁目形_半角数字丁目に正規化される() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都新宿区西新宿一丁目2-3", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
    }

    // -------------------------------------------------------
    // ParseAsync - 住所補正（市区名省略）
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_市区名省略_ward単体でマッチしてCorrectedがtrue() {
        var result = await this._sut.ParseAsync("大阪府北区梅田");

        Assert.NotNull(result);
        Assert.Equal("大阪府", result.Prefecture.Name);
        Assert.Equal("大阪市北区", result.City.DisplayName);
        Assert.True(result.Corrected);
        Assert.Equal("梅田", result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_市区町村完全省略_町字から市区町村を逆引きしてCorrectedがtrue() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都西新宿1-2-3", options);

        Assert.NotNull(result);
        Assert.Equal("東京都", result.Prefecture.Name);
        Assert.Equal("新宿区", result.City.Name);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
        Assert.True(result.Corrected);
    }

    [Fact]
    public async Task ParseAsync_都道府県名重複入力_nullを返す() {
        // "東京都" が重複 → 市区町村が特定できないためnull
        // 渋谷区の1文字町字「東」への誤マッチを防ぐ回帰テスト
        var result = await this._sut.ParseAsync("東京都東京都新宿区西新宿1-2-3");
        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_通常マッチ_CorrectedがFalse() {
        var result = await this._sut.ParseAsync("東京都新宿区西新宿");

        Assert.NotNull(result);
        Assert.False(result.Corrected);
    }

    // -------------------------------------------------------
    // ParseAsync - BestEffort（住所抽出）
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_BestEffort_先頭以外の住所を抽出してOffsetを返す() {
        var options = new AddressParseOptions { BestEffort = true };
        var result = await this._sut.ParseAsync("勤務地：東京都新宿区西新宿", options);

        Assert.NotNull(result);
        Assert.Equal("東京都", result.Prefecture.Name);
        Assert.Equal("新宿区", result.City.Name);
        Assert.Equal("西新宿", result.Remainder);
        Assert.Equal(4, result.Offset); // "勤務地：" = 4文字
    }

    [Fact]
    public async Task ParseAsync_BestEffort_先頭から始まる場合Offsetは0() {
        var options = new AddressParseOptions { BestEffort = true };
        var result = await this._sut.ParseAsync("東京都新宿区西新宿", options);

        Assert.NotNull(result);
        Assert.Equal(0, result.Offset);
    }

    [Fact]
    public async Task ParseAsync_BestEffortFalse_先頭以外の住所はnullを返す() {
        var result = await this._sut.ParseAsync("勤務地：東京都新宿区西新宿");

        Assert.Null(result);
    }

    // -------------------------------------------------------
    // ParseAsync - 郡（county）を含む市区町村
    // -------------------------------------------------------
    // -------------------------------------------------------
    // ParseAsync - 郡（county）を含む市区町村
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_郡あり市区町村_正しく特定される() {
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町大字吉野山123-4");

        Assert.NotNull(result);
        Assert.Equal("奈良県", result.Prefecture.Name);
        Assert.Equal("吉野郡吉野町", result.City.Name);
        Assert.Equal("吉野郡吉野町", result.City.DisplayName);
        Assert.Equal("吉野郡", result.City.County);
    }

    [Fact]
    public async Task ParseAsync_SplitRemainder_郡あり丁目なし地区_BlockにセットされRemainderが空() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町大字吉野山123-4", options);

        Assert.NotNull(result);
        Assert.Equal("大字吉野山", result.Town?.Name);
        Assert.Null(result.Street);
        Assert.Equal("123-4", result.Block);
        Assert.Equal(string.Empty, result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_郡省略_CorrectedがTrueで郡名が補完される() {
        var result = await this._sut.ParseAsync("奈良県吉野町大字吉野山");

        Assert.NotNull(result);
        Assert.Equal("奈良県", result.Prefecture.Name);
        Assert.Equal("吉野郡吉野町", result.City.Name);
        Assert.True(result.Corrected);
        Assert.Equal("大字吉野山", result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_郡省略_SplitRemainder_町字まで正しくパースされる() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("奈良県吉野町大字吉野山123-4", options);

        Assert.NotNull(result);
        Assert.Equal("吉野郡吉野町", result.City.Name);
        Assert.Equal("大字吉野山", result.Town?.Name);
        Assert.Equal("123-4", result.Block);
        Assert.True(result.Corrected);
    }

    // -------------------------------------------------------
    // ParseAsync - NormalizeOaza（大字正規化）
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_SplitRemainder_番地の後に建物名_BlockとRemainderに分離される() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都新宿区西新宿1-2-3新宿NSビル", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
        Assert.Equal("新宿NSビル", result.Remainder);
    }

    [Theory]
    [InlineData("東京都新宿区新宿3丁目1番20号新宿ビル3F",  "1-20", "新宿ビル3F")]  // 番M号
    [InlineData("東京都新宿区新宿3丁目1番地20号新宿ビル3F","1-20", "新宿ビル3F")]  // 番地M号
    [InlineData("東京都新宿区新宿3丁目1番20新宿ビル3F",   "1-20", "新宿ビル3F")]   // 号なし
    public async Task ParseAsync_SplitRemainder_番号形式の番地_ハイフン区切りに正規化される(
        string address, string expectedBlock, string expectedRemainder) {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("新宿", result.Town?.Name);
        Assert.Equal("3丁目", result.Street);
        Assert.Equal(expectedBlock, result.Block);
        Assert.Equal(expectedRemainder, result.Remainder);
    }

    [Theory]
    [InlineData("東京都新宿区西新宿1-2-3 新宿NSビル")]   // 半角スペース
    [InlineData("東京都新宿区西新宿1-2-3　新宿NSビル")]  // 全角スペース
    public async Task ParseAsync_SplitRemainder_番地と建物名の間のスペースはTrimされる(string address) {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("2-3", result.Block);
        Assert.Equal("新宿NSビル", result.Remainder);
    }

    [Theory]
    [InlineData("東京都千代田区二番町7番地5平和ビル5階",  "7-5", "平和ビル5階")]  // N番地M
    [InlineData("東京都千代田区二番町7番5号平和ビル5階",  "7-5", "平和ビル5階")]  // N番M号
    [InlineData("東京都千代田区二番町7番地平和ビル5階",   "7",   "平和ビル5階")]  // N番地（号なし）
    public async Task ParseAsync_SplitRemainder_丁目なし地区で番地形式の番地_streetがnullでblockが正規化される(
        string address, string expectedBlock, string expectedRemainder) {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("二番町", result.Town?.Name);
        Assert.Null(result.Street);
        Assert.Equal(expectedBlock, result.Block);
        Assert.Equal(expectedRemainder, result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_SplitRemainder_丁目なし地区でハイフンなし番地_Blockにセットされる() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町大字吉野山567", options);

        Assert.NotNull(result);
        Assert.Equal("大字吉野山", result.Town?.Name);
        Assert.Null(result.Street);
        Assert.Equal("567", result.Block);
        Assert.Equal(string.Empty, result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_NormalizeOaza無効_大字なし入力は大字ありデータに一致しない() {
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = false };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町吉野山123-4", options);

        // 「吉野山」は「大字吉野山」に一致しない → Town が null
        Assert.NotNull(result);
        Assert.Null(result.Town);
        Assert.Equal("吉野山123-4", result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_NormalizeOaza有効_大字なし入力が大字ありにマッチして正規化される() {
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町吉野山123-4", options);

        Assert.NotNull(result);
        Assert.Equal("大字吉野山", result.Town?.Name);
        Assert.Null(result.Street);
        Assert.Equal("123-4", result.Block);
    }

    [Fact]
    public async Task ParseAsync_NormalizeNumberFalse_全角のままRemainderに残る() {
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeNumber = false };
        var result = await this._sut.ParseAsync("東京都新宿区西新宿１丁目２－３", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        // 全角のままなのでStreet/Blockは分割されずRemainderに残る
        Assert.Null(result.Street);
        Assert.Null(result.Block);
    }
}
