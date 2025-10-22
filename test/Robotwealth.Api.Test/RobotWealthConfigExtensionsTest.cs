using Microsoft.Extensions.Configuration;
using RobotWealth.Api.Extensions;
using Shouldly;

namespace RobotWealth.Api.Test;

public class RobotWealthConfigExtensionsTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenGoodConfig_ShouldReturnHasRobotWealthConfig(bool hasRwConfig)
    {
        // arrange
        var builder = new ConfigurationBuilder();
        if (hasRwConfig)
        {
            builder.AddJsonFile("appsettings.json");
            builder.AddJsonFile($"appsettings.local.json");
        }

        var config = builder.Build();

        // act
        var result = config.HasRobotWealthConfig();

        // assert
        result.ShouldBe(hasRwConfig);
    }

    [Fact]
    public void GivenGoodConfig_ShouldGetRobotWealthConfig()
    {
        // arrange
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.local.json", true, true);
        var config = builder.Build();

        // act
        var robotWealthConfig = config.GetRobotWealthConfig();

        // assert
        robotWealthConfig.ShouldNotBeNull();
        robotWealthConfig.VolatilitiesUrlPath.ShouldNotBeNullOrEmpty();
        robotWealthConfig.WeightsUrlPath.ShouldNotBeNullOrEmpty();
    }
}