using GLMS.Api.Infrastructure;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/service-requests")]
[Produces("application/json")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _requests;

    public ServiceRequestsController(IServiceRequestService requests) => _requests = requests;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestDto>>> GetAll([FromQuery] int? contractId, CancellationToken ct) =>
        Ok(await _requests.GetAllAsync(contractId, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceRequestDto>> GetById(int id, CancellationToken ct)
    {
        var request = await _requests.GetByIdAsync(id, ct);
        return request is null ? Problem(statusCode: 404, title: $"Service request {id} was not found.") : Ok(request);
    }

    /// <summary>
    /// Raises a service request. Rejected with 400 unless the contract is Active and in term.
    /// The ZAR cost is calculated from the live USD to ZAR rate (503 if no rate is available).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceRequestDto>> Create(CreateServiceRequestDto input, CancellationToken ct)
    {
        var result = await _requests.CreateAsync(input, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ToProblem(result);
    }

    /// <summary>Approve / decline / complete: Pending -> Approved | Declined, Approved -> Completed.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceRequestDto>> ChangeStatus(int id, ChangeServiceRequestStatusDto input, CancellationToken ct)
    {
        var result = await _requests.ChangeStatusAsync(id, input.Status, ct);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }
}
