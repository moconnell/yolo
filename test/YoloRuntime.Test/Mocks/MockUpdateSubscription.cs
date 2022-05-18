using CryptoExchange.Net.Sockets;

namespace YoloRuntime.Test.Mocks;

internal class MockUpdateSubscription : UpdateSubscription
{
    internal MockUpdateSubscription() : base(MockSocketConnection.Instance,
        SocketSubscription.CreateForRequest(
            0,
            new object(),
            true,
            e => { }))
    {
    }
}