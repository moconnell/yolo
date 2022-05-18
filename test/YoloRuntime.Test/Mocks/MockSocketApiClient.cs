using CryptoExchange.Net;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;

namespace YoloRuntime.Test.Mocks;

internal class MockSocketApiClient : SocketApiClient
{
    public static readonly MockSocketApiClient Instance = new();

    private MockSocketApiClient() : base(new BaseClientOptions(), new ApiClientOptions())
    {
    }

    protected override AuthenticationProvider CreateAuthenticationProvider(ApiCredentials credentials) =>
        new MockAuthenticationProvider(credentials);
}