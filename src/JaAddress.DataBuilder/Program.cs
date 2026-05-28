using System.CommandLine;
using JaAddress.DataBuilder.Commands;
using JaAddress.DataBuilder.Downloaders;
using JaAddress.DataBuilder.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// -------------------------------------------------------
// CLI オプション定義
// -------------------------------------------------------
var outputOption = new Option<string>("--output") {
    Description = "データの出力先ディレクトリ",
    DefaultValueFactory = _ => Path.Combine(Directory.GetCurrentDirectory(), "data"),
    Required = true,
    Aliases = { "-o" },
};
var prefOption = new Option<string?>("--pref") {
    Description = "取得対象の都道府県名（部分一致）。省略時は全都道府県を取得。例: --pref 東京都",
    Required = false,
    Aliases = { "-p" },
};
var verboseOption = new Option<bool>("--verbose") {
    Description = "デバッグログを出力する",
    DefaultValueFactory = _ => false,
    Aliases = { "-v" },
};
var rootCommand = new RootCommand("JaAddress データ構築ツール - Geolonia 住所データをローカルに保存します") {
    outputOption,
    prefOption,
    verboseOption,
};

// -------------------------------------------------------
// コマンドハンドラ
// -------------------------------------------------------
rootCommand.SetAction((ParseResult parseResult, CancellationToken cancellationToken) => {
    // parseResult から値を取得
    var output  = parseResult.GetValue(outputOption)!;
    var pref    = parseResult.GetValue(prefOption);
    var verbose = parseResult.GetValue(verboseOption);

    // DI コンテナ構築
    var services = new ServiceCollection();
    services.AddLogging(logging => {
        logging.AddConsole();
        logging.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
    });
    services.AddHttpClient<GeoloniaDataDownloader>(client => {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "JaAddress-DataBuilder/1.0 (https://github.com/your-org/JaAddress)");
    });
    services.AddSingleton<JsonDataProcessor>();
    services.AddSingleton<BuildDataCommand>();

    // async ラムダの代わりに Task を返すローカル関数
    return RunAsync(services, output, pref, cancellationToken);
});

return await rootCommand.Parse(args).InvokeAsync();

// -------------------------------------------------------
// メイン処理（async は SetAction の外に切り出す）
// -------------------------------------------------------
static async Task<int> RunAsync(
    ServiceCollection services,
    string output,
    string? pref,
    CancellationToken cancellationToken)
{
    await using var provider = services.BuildServiceProvider();
    var command = provider.GetRequiredService<BuildDataCommand>();
    var logger  = provider.GetRequiredService<ILogger<Program>>();

    var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    Console.CancelKeyPress += (_, e) => {
        e.Cancel = true;
        logger.LogWarning("キャンセルを受け付けました。処理を中断します...");
        cts.Cancel();
    };

    try {
        await command.ExecuteAsync(output, pref, cts.Token);
        return 0;
    } catch (OperationCanceledException) {
        logger.LogWarning("処理がキャンセルされました。");
        return 1;
    } catch (Exception ex) {
        logger.LogError(ex, "予期しないエラーが発生しました。");
        return 1;
    }
}

// Program クラス（テスト用 WebApplicationFactory から参照できるよう partial で定義）
public partial class Program { }
