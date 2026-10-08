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

    [Theory]
    [InlineData("東京都新宿区西新宿西新宿1-2-3")]         // 町名が2連続
    [InlineData("東京都新宿区西新宿西新宿西新宿1-2-3")]   // 町名が3連続
    public async Task ParseAsync_SplitRemainder_町名が連続重複_重複を読み飛ばして分割する(string address) {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
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

    [Fact]
    public async Task ParseAsync_BestEffort_都道府県が複数出現_最も詳細に解析できた候補を採用する() {
        // 会社名の括弧内に別の「東京都○○区」が紛れ込むケース。
        // 先頭側は町字が取れず、後続の完全な住所を採用する。
        var options = new AddressParseOptions { BestEffort = true, SplitRemainder = true };
        var result = await this._sut.ParseAsync(
            "ハーベスト株式会社(東京都千代田区内の社員食堂) 東京都新宿区西新宿1-2-3 地図を見る", options);

        Assert.NotNull(result);
        Assert.Equal("東京都", result.Prefecture.Name);
        Assert.Equal("新宿区", result.City.Name);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
    }

    [Fact]
    public async Task ParseAsync_BestEffort_都道府県のみのノイズが先行_後続の完全な住所を採用する() {
        var options = new AddressParseOptions { BestEffort = true, SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都で募集中 東京都新宿区西新宿1-2-3", options);

        Assert.NotNull(result);
        Assert.Equal("西新宿", result.Town?.Name);
        Assert.Equal("1丁目", result.Street);
        Assert.Equal("2-3", result.Block);
    }

    [Fact]
    public async Task ParseAsync_BestEffort_同レベルの候補が複数_先に出現した方を採用する() {
        // どちらも町字まで取れない場合は従来どおり最初の出現位置を優先する
        var options = new AddressParseOptions { BestEffort = true };
        var result = await this._sut.ParseAsync("東京都新宿区 または 東京都千代田区", options);

        Assert.NotNull(result);
        Assert.Equal("新宿区", result.City.Name);
        Assert.Equal(0, result.Offset);
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

    // -------------------------------------------------------
    // ParseAsync - 丁目省略形で辞書にない丁目番号（番地の先頭の数字を落とさない）
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_SplitRemainder_辞書にない丁目番号_丁目とみなさず番地全体をBlockにする() {
        // 歌舞伎町は一丁目・二丁目のみ。"13-9" は 13丁目ではなく番地 "13-9"（修正前は先頭の "13" を消費し Block="9" だった）
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都新宿区歌舞伎町13-9", options);

        Assert.NotNull(result);
        Assert.Equal("歌舞伎町", result.Town?.Name);
        Assert.Null(result.Street);
        Assert.Equal("13-9", result.Block);
        Assert.Equal(string.Empty, result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_SplitRemainder_辞書にある丁目番号の省略形_従来どおり丁目と番地に分割する() {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync("東京都新宿区歌舞伎町2-5-1", options);

        Assert.NotNull(result);
        Assert.Equal("歌舞伎町", result.Town?.Name);
        Assert.Equal("2丁目", result.Street);
        Assert.Equal("5-1", result.Block);
    }

    // -------------------------------------------------------
    // ParseAsync - NormalizeOaza の大字を外した一致と、より長い町字の優先順位
    // -------------------------------------------------------
    [Fact]
    public async Task ParseAsync_NormalizeOaza_より長く一致する町字を大字を外した一致より優先する() {
        // 「大字本城」（大字を外すと「本城」＝2文字）より「本城東」（3文字）の方が長く一致する
        // （修正前は Name.Length の長い「大字本城」が先に一致し、"東2丁目1-21" が Remainder になっていた）
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町本城東2丁目1-21", options);

        Assert.NotNull(result);
        Assert.Equal("本城東", result.Town?.Name);
        Assert.Equal("2丁目", result.Street);
        Assert.Equal("1-21", result.Block);
    }

    [Fact]
    public async Task ParseAsync_NormalizeOaza_大字を外した一致で正規形の大字付き町字を返す() {
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町吉野山123-4", options);

        Assert.NotNull(result);
        Assert.Equal("大字吉野山", result.Town?.Name);
        Assert.Equal("123-4", result.Block);
    }

    [Fact]
    public async Task ParseAsync_NormalizeOaza_消費文字数が同じ場合は従来どおり名前の長い町字を優先する() {
        // 「本城」（入力どおり）と「大字本城」（大字を外して一致）はどちらも2文字消費。従来どおり Name.Length の長い「大字本城」を返す
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町本城123", options);

        Assert.NotNull(result);
        Assert.Equal("大字本城", result.Town?.Name);
        Assert.Equal("123", result.Block);
    }

    // -------------------------------------------------------
    // ParseAsync - 小字
    // -------------------------------------------------------
    [Theory]
    // 辞書の小字と一致（他の小字「字大地」より長く一致する「字大地内」を選ぶ）
    [InlineData("奈良県吉野郡吉野町大字六田字大地内95-5", "字大地内", "95-5", "")]
    // 入力が「字」を省略、辞書は「字」付き
    [InlineData("奈良県吉野郡吉野町大字六田大地内95-5", "字大地内", "95-5", "")]
    // 入力が「字」付き、辞書は「字」なし
    [InlineData("奈良県吉野郡吉野町大字六田字中島12", "中島", "12", "")]
    // 「字」の付かない小字
    [InlineData("奈良県吉野郡吉野町六田中島12-3ハイツ101", "中島", "12-3", "ハイツ101")]
    // 全角数字・ハイフン
    [InlineData("奈良県吉野郡吉野町大字六田字大地内９５－５", "字大地内", "95-5", "")]
    // 辞書にない「字○○」は入力の表記
    [InlineData("奈良県吉野郡吉野町大字六田字新田7-8", "字新田", "7-8", "")]
    // 辞書の小字「字東」が前半だけに一致する場合は、辞書にない「字東山」として読む
    [InlineData("奈良県吉野郡吉野町大字六田字東山5", "字東山", "5", "")]
    // 「字」で始まる辞書の小字は、番地がなくても読み取る
    [InlineData("奈良県吉野郡吉野町大字六田字宮前ハイツ", "字宮前", null, "ハイツ")]
    [InlineData("奈良県吉野郡吉野町大字六田字大地内", "字大地内", null, "")]
    public async Task ParseAsync_小字のあとの番地を取得し小字をKoazaに返す(
        string address, string expectedKoaza, string? expectedBlock, string expectedRemainder) {

        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("大字六田", result.Town?.Name);
        Assert.Equal(expectedKoaza, result.Koaza);
        Assert.Null(result.Street);
        Assert.Equal(expectedBlock, result.Block);
        Assert.Equal(expectedRemainder, result.Remainder);
    }

    // -------------------------------------------------------
    // ParseAsync - 町字名・小字名の数字の表記
    // -------------------------------------------------------
    [Theory]
    // 辞書は漢数字、入力は算用数字（全角は NormalizeNumber で半角になる）
    [InlineData("奈良県吉野郡吉野町美園2条1丁目2", "美園二条", "1丁目", "2")]
    [InlineData("奈良県吉野郡吉野町美園２条１丁目２", "美園二条", "1丁目", "2")]
    [InlineData("奈良県吉野郡吉野町北1条西2丁目3", "北一条西", "2丁目", "3")]
    [InlineData("奈良県吉野郡吉野町古町通5番町615", "古町通五番町", null, "615")]
    // 辞書どおりの漢数字の入力も従来どおり一致する
    [InlineData("奈良県吉野郡吉野町美園二条1丁目2", "美園二条", "1丁目", "2")]
    // "N丁" で終わる町字（蔵前町二丁）は "丁目" の途中まで一致させない
    [InlineData("奈良県吉野郡吉野町蔵前町2丁目8-5", "蔵前町", "2丁目", "8-5")]
    [InlineData("奈良県吉野郡吉野町蔵前町2丁8-5", "蔵前町二丁", null, "8-5")]
    public async Task ParseAsync_町字名の漢数字を算用数字で書いた入力も一致する(
        string address, string expectedTown, string? expectedStreet, string expectedBlock) {

        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal(expectedTown, result.Town?.Name);
        Assert.Equal(expectedStreet, result.Street);
        Assert.Equal(expectedBlock, result.Block);
    }

    [Theory]
    // 札幌の "十一丁目北"：丁目 "11丁目"＋番地なしではなく、小字として読み番地を取る
    [InlineData("奈良県吉野郡吉野町平和通11丁目北6-19", "平和通", "十一丁目北", "6-19")]
    [InlineData("奈良県吉野郡吉野町平和通十一丁目南5-5", "平和通", "十一丁目南", "5-5")]
    // 岩手の地割（辞書は全角数字）：地割の数字を番地として読まない。"１地割" より長く一致する "１４地割" を選ぶ
    [InlineData("奈良県吉野郡吉野町村崎野14地割453-5", "村崎野", "１４地割", "453-5")]
    [InlineData("奈良県吉野郡吉野町村崎野1地割5", "村崎野", "１地割", "5")]
    public async Task ParseAsync_数字を含む辞書の小字は丁目番地より優先して読む(
        string address, string expectedTown, string expectedKoaza, string expectedBlock) {

        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal(expectedTown, result.Town?.Name);
        Assert.Equal(expectedKoaza, result.Koaza);
        Assert.Null(result.Street);
        Assert.Equal(expectedBlock, result.Block);
    }

    [Fact]
    public async Task ParseAsync_数字を含む小字でも直後に番地が続かなければ小字とみなさない() {
        // 辞書の小字「八反田」が「八反田町1」の前半だけに一致する場合は読み取らない
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町来迎寺八反田町1", options);

        Assert.NotNull(result);
        Assert.Equal("来迎寺", result.Town?.Name);
        Assert.Null(result.Koaza);
        Assert.Equal("八反田町1", result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_地番の冠称を小字として読み番地を取る() {
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町来迎寺甲2621-4", options);

        Assert.NotNull(result);
        Assert.Equal("来迎寺", result.Town?.Name);
        Assert.Equal("甲", result.Koaza);
        Assert.Equal("2621-4", result.Block);
    }

    [Theory]
    // 小字のあとの "1-1" は丁目省略形として読まない（小字のある地域に丁目はない）
    [InlineData("奈良県吉野郡吉野町北崎町井田1-1", "井田", null, "1-1")]
    // 小字がなければ従来どおり丁目省略形として読む
    [InlineData("奈良県吉野郡吉野町北崎町1-1", null, "1丁目", "1")]
    public async Task ParseAsync_小字のあとの番地は丁目省略形として読まない(
        string address, string? expectedKoaza, string? expectedStreet, string expectedBlock) {

        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync(address, options);

        Assert.NotNull(result);
        Assert.Equal("北崎町", result.Town?.Name);
        Assert.Equal(expectedKoaza, result.Koaza);
        Assert.Equal(expectedStreet, result.Street);
        Assert.Equal(expectedBlock, result.Block);
    }

    [Fact]
    public async Task ParseAsync_字の付かない小字のあとに番地がなければ小字とみなさない() {
        // 「中島」は辞書の小字だが、建物名等の先頭と区別できないため読み取らない
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町大字六田中島ハイツ", options);

        Assert.NotNull(result);
        Assert.Equal("大字六田", result.Town?.Name);
        Assert.Null(result.Koaza);
        Assert.Null(result.Block);
        Assert.Equal("中島ハイツ", result.Remainder);
    }

    [Fact]
    public async Task ParseAsync_町字の直後が番地なら小字を読み取らない() {
        var options = new AddressParseOptions { SplitRemainder = true, NormalizeOaza = true };
        var result = await this._sut.ParseAsync("奈良県吉野郡吉野町大字六田12-3", options);

        Assert.NotNull(result);
        Assert.Null(result.Koaza);
        Assert.Equal("12-3", result.Block);
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

    // -------------------------------------------------------
    // キャッシュ有効性
    // -------------------------------------------------------
    [Fact]
    public async Task GetPrefecturesAsync_2回呼び出し_同一インスタンスを返す() {
        var first = await this._sut.GetPrefecturesAsync();
        var second = await this._sut.GetPrefecturesAsync();

        Assert.True(ReferenceEquals(first, second));
    }

    [Fact]
    public async Task GetCitiesAsync_同一都道府県を2回呼び出し_同一インスタンスを返す() {
        var first = await this._sut.GetCitiesAsync("東京都");
        var second = await this._sut.GetCitiesAsync("東京都");

        Assert.True(ReferenceEquals(first, second));
    }

    [Fact]
    public async Task ParseAsync_複数回呼び出し後のGetCitiesAsync_キャッシュ済みインスタンスを返す() {
        // ParseAsync 内部でも GetCitiesAsync が呼ばれるため、
        // ParseAsync 呼び出し後も GetCitiesAsync が同一インスタンスを返すことを確認する
        var before = await this._sut.GetCitiesAsync("東京都");

        await this._sut.ParseAsync("東京都新宿区西新宿1丁目2-3");
        await this._sut.ParseAsync("東京都千代田区丸の内1丁目");

        var after = await this._sut.GetCitiesAsync("東京都");

        Assert.True(ReferenceEquals(before, after));
    }

    // -------------------------------------------------------
    // ParseWithReasonAsync（失敗理由付き）
    // -------------------------------------------------------
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseWithReasonAsync_解析成功_ResultとNoneを返す(bool bestEffort) {
        var options = new AddressParseOptions { BestEffort = bestEffort };
        var outcome = await this._sut.ParseWithReasonAsync("東京都新宿区西新宿", options);

        Assert.Equal(AddressParseFailureReason.None, outcome.FailureReason);
        Assert.NotNull(outcome.Result);
        Assert.Equal("新宿区", outcome.Result.City.Name);
    }

    [Theory]
    [InlineData("存在しない県どこか市", false)]
    [InlineData("存在しない県どこか市", true)]
    [InlineData("", false)]
    [InlineData("", true)]
    // BestEffort=false では先頭が都道府県名で始まらなければ都道府県なし
    [InlineData("勤務地：東京都新宿区西新宿", false)]
    public async Task ParseWithReasonAsync_都道府県を特定できない_PrefectureNotFoundを返す(string address, bool bestEffort) {
        var options = new AddressParseOptions { BestEffort = bestEffort };
        var outcome = await this._sut.ParseWithReasonAsync(address, options);

        Assert.Null(outcome.Result);
        Assert.Equal(AddressParseFailureReason.PrefectureNotFound, outcome.FailureReason);
    }

    [Theory]
    [InlineData("東京都", false)]
    [InlineData("東京都", true)]
    [InlineData("東京都存在しない市", false)]
    [InlineData("勤務地：東京都存在しない市", true)]
    // BestEffort=true で都道府県名が複数出現し、すべての出現位置で市区町村を特定できない
    [InlineData("東京都不明 大阪府不明", true)]
    public async Task ParseWithReasonAsync_市区町村を特定できない_CityNotFoundを返す(string address, bool bestEffort) {
        var options = new AddressParseOptions { BestEffort = bestEffort };
        var outcome = await this._sut.ParseWithReasonAsync(address, options);

        Assert.Null(outcome.Result);
        Assert.Equal(AddressParseFailureReason.CityNotFound, outcome.FailureReason);
    }

    [Theory]
    [InlineData("東京都新宿区西新宿1丁目2-3", true)]
    [InlineData("存在しない県どこか市", false)]
    [InlineData("東京都存在しない市", false)]
    public async Task ParseAsync_ParseWithReasonAsyncと同じ解析結果を返す(string address, bool expectedSuccess) {
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await this._sut.ParseAsync(address, options);
        var outcome = await this._sut.ParseWithReasonAsync(address, options);

        Assert.Equal(expectedSuccess, result is not null);
        Assert.Equal(result is not null, outcome.Result is not null);
        Assert.Equal(result?.City.Name, outcome.Result?.City.Name);
        Assert.Equal(result?.Town?.Name, outcome.Result?.Town?.Name);
        Assert.Equal(result?.Street, outcome.Result?.Street);
        Assert.Equal(result?.Block, outcome.Result?.Block);
    }
}
