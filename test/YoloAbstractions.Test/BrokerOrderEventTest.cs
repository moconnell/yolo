using YoloAbstractions;

namespace YoloAbstractions.Test;

public class BrokerOrderEventTest
{
    [Fact]
    public void Constructor_ShouldPopulateProperties()
    {
        var order = new Order(
            Id: 42,
            Symbol: "SOL",
            AssetType: AssetType.Future,
            Created: DateTime.UtcNow,
            OrderSide: OrderSide.Buy,
            OrderStatus: OrderStatus.Open,
            Amount: 1.5m,
            Filled: 0.5m,
            LimitPrice: 100m,
            ClientId: "c1");

        var evt = new BrokerOrderEvent("c1", order, Success: false, Error: "failed", ErrorCode: 500);

        evt.ClientOrderId.ShouldBe("c1");
        evt.Order.ShouldBe(order);
        evt.Success.ShouldBeFalse();
        evt.Error.ShouldBe("failed");
        evt.ErrorCode.ShouldBe(500);
    }
}
