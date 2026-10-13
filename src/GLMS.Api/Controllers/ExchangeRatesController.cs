using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exchange-rates")]
[Produces("application/json")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _rates;

    public ExchangeRatesController(IExchangeRateService rates) => _rates = rates;

    /// <summary>Current USD to ZAR rate from ExchangeRate-API (cached; IsStale=true if the live API is down).</summary>
    [HttpGet("usd-zar")]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ExchangeRateDto>> GetUsdToZar(CancellationToken ct)
    {
        var rate = await _rates.GetUsdToZarAsync(ct);
        return rate is null
            ? Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Exchange rate service unavailable.")
            : Ok(rate);
    }
}
