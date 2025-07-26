using YoloBroker.Hyperliquid.CustomSigning;
using Shouldly;

using static System.Text.Encoding;

namespace YoloBroker.Hyperliquid.Test.CustomSigning;

public class NethereumSigningExtensionsTest
{
    [Fact]
    public void SignMessage_ShouldReturnSignature_ForValidInput()
    {
        // Arrange
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var message = System.Convert.ToHexString(UTF8.GetBytes("Hello, Hyperliquid!"));

        // Act
        var signature = message.SignMessage(privateKey);

        // Assert
        signature.ShouldNotBeNull();
        signature.ShouldContainKey("r");
        signature.ShouldContainKey("s");
        signature.ShouldContainKey("v");
        signature["r"].ToString().ShouldStartWith("0x");
        signature["s"].ToString().ShouldStartWith("0x");
        ((int)signature["v"]).ShouldBeInRange(27, 28); // Valid V values for Ethereum signatures
        signature["r"].ToString()!.Length.ShouldBe(66); // 32 bytes in hex + "0x"
        signature["s"].ToString()!.Length.ShouldBe(66); // 32 bytes in hex + "0x"
    }
}
