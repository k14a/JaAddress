using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Core;

/// <summary>JaAddress のサービスを DI コンテナへ登録する拡張メソッド。</summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// <see cref="IAddressService"/> と <see cref="IItaijiFolder"/> をシングルトンとして登録する。
    /// </summary>
    /// <param name="services">登録先のサービスコレクション。</param>
    /// <param name="options">設定。<see cref="JaAddressOptions.DataDirectory"/> に DataBuilder が出力したデータの場所を指定する。</param>
    /// <returns>メソッドチェーン用に <paramref name="services"/> をそのまま返す。</returns>
    public static IServiceCollection AddJaAddress(
        this IServiceCollection services,
        JaAddressOptions options) {

        services.AddSingleton(options);
        services.AddSingleton<IItaijiFolder, ItaijiFolder>();
        services.AddSingleton<IAddressService, AddressService>();
        return services;
    }
}
