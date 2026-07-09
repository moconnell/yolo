using System.Net;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using Shouldly;
using YoloBroker.Hyperliquid.Exceptions;

namespace YoloBroker.Hyperliquid.Test.Exceptions;

public class HyperliquidExceptionTest
{
    [Fact]
    public void Constructor_WithErrorCode_ShouldIncludeCode()
    {
        var exception = new HyperliquidException("Could not place order", 123);

        exception.Message.ShouldBe("Could not place order (123)");
    }

    [Fact]
    public void Constructor_WithCallResult_ShouldIncludeError()
    {
        var error = Error("Call failed");
        var result = CallResult.Fail(error);

        var exception = new HyperliquidException("Could not call API", result);

        exception.Message.ShouldBe($"Could not call API - {error}");
    }

    [Fact]
    public void Constructor_WithHttpResult_ShouldIncludeErrorAndStatusCode()
    {
        var error = Error("Bad request");
        var result = HttpResult.Fail(
            string.Empty,
            HttpStatusCode.BadRequest,
            new Version(1, 1),
            null!,
            TimeSpan.Zero,
            null,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            HttpMethod.Get,
            null!,
            ResultDataSource.Server,
            error,
            false);

        var exception = new HyperliquidException("Could not get orders", result);

        exception.Message.ShouldBe($"Could not get orders - {error} (BadRequest)");
    }

    [Fact]
    public void Constructor_WithWebSocketResult_ShouldIncludeError()
    {
        var error = Error("Subscription failed");
        var result = WebSocketResult.Fail("HyperLiquid", error);

        var exception = new HyperliquidException("Could not subscribe", result);

        exception.Message.ShouldBe($"Could not subscribe - {error}");
    }

    private static Error Error(string message) =>
        new TestError(
            "TestError",
            new ErrorInfo(ErrorType.Unknown, message)
            {
                Message = message
            },
            null);

    private class TestError(string? errorCode, ErrorInfo errorInfo, Exception? exception)
        : Error(
            errorCode,
            errorInfo,
            exception);
}
