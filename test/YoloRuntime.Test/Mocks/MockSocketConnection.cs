using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Sockets;
using Moq;

namespace YoloRuntime.Test.Mocks;

internal class MockSocketConnection : SocketConnection
{
    public static readonly MockSocketConnection Instance = new();

    private MockSocketConnection() : base(
        MockSocketClient.Instance,
        MockSocketApiClient.Instance,
        new Mock<IWebsocket>().Object)
    {
    }
}