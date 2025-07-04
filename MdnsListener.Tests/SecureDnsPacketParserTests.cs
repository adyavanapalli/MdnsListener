using Microsoft.Extensions.Logging;
using Moq;
using MdnsListener.Models;
using MdnsListener.Services;

namespace MdnsListener.Tests;

public class SecureDnsPacketParserTests
{
    private readonly Mock<ILogger<SecureDnsPacketParser>> _loggerMock;
    private readonly SecureDnsPacketParser _parser;

    public SecureDnsPacketParserTests()
    {
        _loggerMock = new Mock<ILogger<SecureDnsPacketParser>>();
        _parser = new SecureDnsPacketParser(_loggerMock.Object);
    }

    [Fact]
    public void Parse_WithTooSmallBuffer_ReturnsNull()
    {
        // Arrange
        var buffer = new byte[11]; // Less than minimum header size (12)

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WithValidDnsResponse_ReturnsPacket()
    {
        // Arrange - Valid DNS response packet with PTR record
        var buffer = new byte[]
        {
            0x00, 0x00, // Transaction ID
            0x84, 0x00, // Flags (response)
            0x00, 0x00, // Questions
            0x00, 0x01, // Answers
            0x00, 0x00, // Authority
            0x00, 0x00, // Additional
            
            // Answer: PTR record
            0x04, 0x74, 0x65, 0x73, 0x74, // "test"
            0x05, 0x6C, 0x6F, 0x63, 0x61, 0x6C, // "local"
            0x00, // End of name
            0x00, 0x0C, // Type PTR
            0x00, 0x01, // Class IN
            0x00, 0x00, 0x00, 0x78, // TTL = 120
            0x00, 0x02, // Data length = 2
            0xC0, 0x0C  // Pointer to offset 12 (reuses the name)
        };

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsResponse);
        Assert.Single(result.AnswerRecords);
        Assert.Equal(DnsRecordType.PTR, result.AnswerRecords[0].Type);
        Assert.Equal(120u, result.AnswerRecords[0].Ttl);
    }


    [Fact]
    public void Parse_WithCompressionPointerLoop_ReturnsNull()
    {
        // Arrange - Packet with compression pointer creating a loop
        var buffer = new byte[]
        {
            0x00, 0x00, // Transaction ID
            0x84, 0x00, // Flags
            0x00, 0x01, // Questions = 1
            0x00, 0x00, // Answers
            0x00, 0x00, // Authority
            0x00, 0x00, // Additional
            
            // Question with compression loop
            0xC0, 0x0C, // Pointer to itself (offset 12)
            0x00, 0x01, // Type
            0x00, 0x01  // Class
        };

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        Assert.Null(result);
    }
}