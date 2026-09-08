using JaAddress.Core.Options;
using JaAddress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Core;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddJaAddress(
        this IServiceCollection services,
        JaAddressOptions options) {

        services.AddSingleton(options);
        services.AddSingleton<IItaijiFolder, ItaijiFolder>();
        services.AddSingleton<IAddressService, AddressService>();
        return services;
    }
}
