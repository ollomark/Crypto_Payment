using Crypto_Payment.Helpers;
using Crypto_Payment.Models;
using Xunit;

namespace Crypto_Payment.Tests.UnitTests;

public class StatusMapperTests
{
    [Theory]
    [InlineData("completed", "completed")]
    [InlineData("confirmed", "completed")]
    [InlineData("mismatch", "completed")]
    [InlineData("COMPLETED", "completed")]
    [InlineData("Confirmed", "completed")]
    [InlineData("MISMATCH", "completed")]
    public void MapPlisioStatus_CompletedGroup_ReturnsCompleted(string input, string expected)
    {
        var result = StatusMapper.MapPlisioStatus(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("expired", "expired")]
    [InlineData("EXPIRED", "expired")]
    public void MapPlisioStatus_ExpiredGroup_ReturnsExpired(string input, string expected)
    {
        var result = StatusMapper.MapPlisioStatus(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("cancelled", "cancelled")]
    [InlineData("Cancelled", "cancelled")]
    public void MapPlisioStatus_Cancelled_ReturnsCancelled(string input, string expected)
    {
        var result = StatusMapper.MapPlisioStatus(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("error", "error")]
    [InlineData("ERROR", "error")]
    public void MapPlisioStatus_Error_ReturnsError(string input, string expected)
    {
        var result = StatusMapper.MapPlisioStatus(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("NEW")]
    [InlineData("New")]
    public void MapPlisioStatus_New_ReturnsNew(string input)
    {
        Assert.Equal(InvoiceStatus.New, StatusMapper.MapPlisioStatus(input));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("PENDING")]
    [InlineData("Pending")]
    public void MapPlisioStatus_Pending_ReturnsPending(string input)
    {
        Assert.Equal(InvoiceStatus.Pending, StatusMapper.MapPlisioStatus(input));
    }

    [Fact]
    public void MapPlisioStatus_Null_ReturnsPending()
    {
        Assert.Equal(InvoiceStatus.Pending, StatusMapper.MapPlisioStatus(null));
    }

    [Fact]
    public void MapPlisioStatus_EmptyString_ReturnsPending()
    {
        Assert.Equal(InvoiceStatus.Pending, StatusMapper.MapPlisioStatus(""));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("something_random")]
    [InlineData("processing")]
    public void MapPlisioStatus_UnknownStatus_ReturnsPending(string input)
    {
        Assert.Equal(InvoiceStatus.Pending, StatusMapper.MapPlisioStatus(input));
    }
}
