using JaAddress.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JaAddress.Api.Tests;

public sealed class ApiTestFixture : WebApplicationFactory<Program> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.ConfigureServices(services => {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IAddressService));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddSingleton<IAddressService, FakeAddressService>();
        });
    }
}
