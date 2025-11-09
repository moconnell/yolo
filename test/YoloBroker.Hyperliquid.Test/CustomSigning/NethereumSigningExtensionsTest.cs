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

    [Fact]
    public void SignMessage_WhenMessageIsNull_ShouldThrowArgumentException()
    {
        // Arrange
        string? message = null;
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        // Act & Assert
        Should.Throw<ArgumentException>(() => message!.SignMessage(privateKey))
            .ParamName.ShouldBe("message");
    }

    [Fact]
    public void SignMessage_WhenMessageIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        var message = string.Empty;
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        // Act & Assert
        Should.Throw<ArgumentException>(() => message.SignMessage(privateKey))
            .ParamName.ShouldBe("message");
    }

    [Fact]
    public void SignMessage_WhenPrivateKeyIsNull_ShouldThrowArgumentException()
    {
        // Arrange
        var message = System.Convert.ToHexString(UTF8.GetBytes("Hello"));
        string? privateKey = null;

        // Act & Assert
        Should.Throw<ArgumentException>(() => message.SignMessage(privateKey!))
            .ParamName.ShouldBe("privateKey");
    }

    [Fact]
    public void SignMessage_WhenPrivateKeyIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        var message = System.Convert.ToHexString(UTF8.GetBytes("Hello"));
        var privateKey = string.Empty;

        // Act & Assert
        Should.Throw<ArgumentException>(() => message.SignMessage(privateKey))
            .ParamName.ShouldBe("privateKey");
    }

    [Fact]
    public void SignMessage_WhenMessageIsNotValidHex_ShouldThrowArgumentException()
    {
        // Arrange
        var message = "xxGGHHIIJJ"; // Invalid hex characters (starting from position 2)
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => message.SignMessage(privateKey));
        ex.ParamName.ShouldBe("message");
        ex.Message.ShouldContain("Message must be a valid hex string");
    }

    [Fact]
    public void SignMessage_WhenMessageContainsNonHexCharacters_ShouldThrowArgumentException()
    {
        // Arrange
        var message = "xx123xyz"; // Contains non-hex characters after position 2
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => message.SignMessage(privateKey));
        ex.ParamName.ShouldBe("message");
        ex.Message.ShouldContain("Message must be a valid hex string");
    }

    [Fact]
    public void SignMessage_WhenPrivateKeyIsTooShort_ShouldThrowArgumentException()
    {
        // Arrange
        var message = System.Convert.ToHexString(UTF8.GetBytes("Hello"));
        var privateKey = "0x0123456789abcdef"; // Too short

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => message.SignMessage(privateKey));
        ex.ParamName.ShouldBe("privateKey");
        ex.Message.ShouldContain("Private key must be a valid hex string of length 64");
    }

    [Fact]
    public void SignMessage_WhenPrivateKeyIsTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var message = System.Convert.ToHexString(UTF8.GetBytes("Hello"));
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef00"; // Too long

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => message.SignMessage(privateKey));
        ex.ParamName.ShouldBe("privateKey");
        ex.Message.ShouldContain("Private key must be a valid hex string of length 64");
    }

    [Fact]
    public void SignMessage_WhenPrivateKeyIsNotValidHex_ShouldThrowArgumentException()
    {
        // Arrange
        var message = System.Convert.ToHexString(UTF8.GetBytes("Hello"));
        var privateKey = "0xGGHHIIJJ0123456789abcdef0123456789abcdef0123456789abcdef01234567"; // Invalid hex

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => message.SignMessage(privateKey));
        ex.ParamName.ShouldBe("privateKey");
        ex.Message.ShouldContain("Private key must be a valid hex string of length 64");
    }

    [Fact]
    public void SignMessage_WhenPrivateKeyHasCorrectLengthButInvalidHex_ShouldThrowArgumentException()
    {
        // Arrange
        var message = System.Convert.ToHexString(UTF8.GetBytes("Hello"));
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdez"; // 'z' is not hex

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => message.SignMessage(privateKey));
        ex.ParamName.ShouldBe("privateKey");
        ex.Message.ShouldContain("Private key must be a valid hex string of length 64");
    }

    [Fact]
    public void SignMessage_WithUppercaseHexMessage_ShouldReturnSignature()
    {
        // Arrange
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var message = "48656C6C6F"; // "Hello" in uppercase hex (without 0x prefix)

        // Act
        var signature = message.SignMessage(privateKey);

        // Assert
        signature.ShouldNotBeNull();
        signature.ShouldContainKey("r");
        signature.ShouldContainKey("s");
        signature.ShouldContainKey("v");
    }

    [Fact]
    public void SignMessage_WithLowercaseHexMessage_ShouldReturnSignature()
    {
        // Arrange
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var message = "48656c6c6f"; // "Hello" in lowercase hex (without 0x prefix)

        // Act
        var signature = message.SignMessage(privateKey);

        // Assert
        signature.ShouldNotBeNull();
        signature.ShouldContainKey("r");
        signature.ShouldContainKey("s");
        signature.ShouldContainKey("v");
    }

    [Fact]
    public void SignMessage_WithMixedCaseHexMessage_ShouldReturnSignature()
    {
        // Arrange
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var message = "48656C6c6F"; // "Hello" in mixed case hex (without 0x prefix)

        // Act
        var signature = message.SignMessage(privateKey);

        // Assert
        signature.ShouldNotBeNull();
        signature.ShouldContainKey("r");
        signature.ShouldContainKey("s");
        signature.ShouldContainKey("v");
    }

    [Fact]
    public void SignMessage_ShouldReturnLowercaseHexSignature()
    {
        // Arrange
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var message = System.Convert.ToHexString(UTF8.GetBytes("Test"));

        // Act
        var signature = message.SignMessage(privateKey);

        // Assert
        var r = signature["r"].ToString()!;
        var s = signature["s"].ToString()!;

        r.ShouldBe(r.ToLowerInvariant());
        s.ShouldBe(s.ToLowerInvariant());
    }

    [Fact]
    public void SignMessage_WithDifferentMessages_ShouldProduceDifferentSignatures()
    {
        // Arrange
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var message1 = System.Convert.ToHexString(UTF8.GetBytes("Message1"));
        var message2 = System.Convert.ToHexString(UTF8.GetBytes("Message2"));

        // Act
        var signature1 = message1.SignMessage(privateKey);
        var signature2 = message2.SignMessage(privateKey);

        // Assert
        signature1["r"].ShouldNotBe(signature2["r"]);
        signature1["s"].ShouldNotBe(signature2["s"]);
    }

    [Fact]
    public void SignMessage_WithSameMessageAndKey_ShouldProduceSameSignature()
    {
        // Arrange
        var privateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var message = System.Convert.ToHexString(UTF8.GetBytes("Consistent Message"));

        // Act
        var signature1 = message.SignMessage(privateKey);
        var signature2 = message.SignMessage(privateKey);

        // Assert
        signature1["r"].ShouldBe(signature2["r"]);
        signature1["s"].ShouldBe(signature2["s"]);
        signature1["v"].ShouldBe(signature2["v"]);
    }
}
