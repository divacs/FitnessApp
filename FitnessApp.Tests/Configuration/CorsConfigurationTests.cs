using FitnessApp.API.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FitnessApp.Tests.Configuration;

public class CorsConfigurationTests
{
    [Fact]
    public void AddCorsPolicy_WhenAllowedOriginsAreConfigured_ShouldAllowAllConfiguredOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:AllowedOrigins"] = "https://retrofitness.rs,http://localhost:5173"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddCorsPolicy(configuration);

        var policy = services.BuildServiceProvider()
            .GetRequiredService<IOptions<CorsOptions>>()
            .Value
            .GetPolicy(ServiceCollectionExtensions.FrontendCorsPolicy);

        policy.Should().NotBeNull();
        policy!.Origins.Should().BeEquivalentTo("https://retrofitness.rs", "http://localhost:5173");
    }

    [Fact]
    public void AddCorsPolicy_WhenAllowedOriginsAreMissing_ShouldFallbackToFrontendUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:FrontendUrl"] = "https://retrofitness.rs"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddCorsPolicy(configuration);

        var policy = services.BuildServiceProvider()
            .GetRequiredService<IOptions<CorsOptions>>()
            .Value
            .GetPolicy(ServiceCollectionExtensions.FrontendCorsPolicy);

        policy.Should().NotBeNull();
        policy!.Origins.Should().BeEquivalentTo("https://retrofitness.rs");
    }
}
