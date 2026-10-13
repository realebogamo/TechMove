using GLMS.Core.Services;

namespace GLMS.UnitTests;

public class CurrencyConverterTests
{
    private readonly CurrencyConverter _converter = new();

    [Theory]
    [InlineData(100, 18.50, 1850.00)]
    [InlineData(1, 18.2345, 18.23)]
    [InlineData(250.75, 17.9, 4488.43)]   // 4488.425 rounds up
    [InlineData(0.01, 18.5, 0.19)]        // 0.185 rounds away from zero
    [InlineData(1000000, 18.123456, 18123456.00)]
    public void ConvertUsdToZar_AtGivenRate_ReturnsAmountRoundedToCents(decimal usd, decimal rate, decimal expectedZar)
    {
        Assert.Equal(expectedZar, _converter.ConvertUsdToZar(usd, rate));
    }

    [Fact]
    public void ConvertUsdToZar_ZeroAmount_ReturnsZero()
    {
        Assert.Equal(0m, _converter.ConvertUsdToZar(0m, 18.5m));
    }

    [Fact]
    public void ConvertUsdToZar_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _converter.ConvertUsdToZar(-1m, 18.5m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-18.5)]
    public void ConvertUsdToZar_ZeroOrNegativeRate_Throws(decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _converter.ConvertUsdToZar(100m, rate));
    }
}
