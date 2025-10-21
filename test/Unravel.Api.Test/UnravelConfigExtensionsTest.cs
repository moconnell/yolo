using Microsoft.Extensions.Configuration;
using Shouldly;
using Unravel.Api.Extensions;

namespace Unravel.Api.Test;

public class UnravelConfigExtensionsTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenGoodConfig_ShouldReturnHasUnravelConfig(bool hasUnravelConfig)
    {
        // arrange
        var builder = new ConfigurationBuilder();
        if (hasUnravelConfig)
        {
            builder.AddJsonFile("appsettings.json");
            builder.AddJsonFile($"appsettings.local.json");
        }

        var config = builder.Build();

        // act
        var result = config.HasUnravelConfig();

        // assert
        result.ShouldBe(hasUnravelConfig);
    }

    [Fact]
    public void GivenGoodConfig_ShouldGetUnravelConfig()
    {
        // arrange
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.local.json", true, true);
        var config = builder.Build();

        // act
        var unravelConfig = config.GetUnravelConfig();

        // assert
        unravelConfig.ShouldNotBeNull();
        unravelConfig.Factors.ShouldNotBeNull();
        unravelConfig.Factors.ShouldNotBeEmpty();
    }
}