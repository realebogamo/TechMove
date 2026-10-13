namespace GLMS.Core.Services;

public interface ICurrencyConverter
{
    decimal ConvertUsdToZar(decimal amountUsd, decimal usdToZarRate);
}

public class CurrencyConverter : ICurrencyConverter
{
    /// <summary>Converts USD to ZAR, rounded to cents (midpoint away from zero, as in invoicing).</summary>
    public decimal ConvertUsdToZar(decimal amountUsd, decimal usdToZarRate)
    {
        if (amountUsd < 0)
            throw new ArgumentOutOfRangeException(nameof(amountUsd), amountUsd, "Amount cannot be negative.");
        if (usdToZarRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(usdToZarRate), usdToZarRate, "Exchange rate must be greater than zero.");

        return Math.Round(amountUsd * usdToZarRate, 2, MidpointRounding.AwayFromZero);
    }
}
