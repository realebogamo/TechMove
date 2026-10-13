using GLMS.Api.Infrastructure;
using GLMS.Core.Services;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/contracts")]
[Produces("application/json")]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contracts;

    public ContractsController(IContractService contracts) => _contracts = contracts;

    /// <summary>Lists contracts. Filter by date range (term overlaps From..To), status, client and text.</summary>
    /// <remarks>Example: GET /api/contracts?status=Active&amp;from=2026-01-01&amp;to=2026-12-31</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ContractDto>>> Search([FromQuery] ContractFilterDto filter, CancellationToken ct)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.To < filter.From)
        {
            ModelState.AddModelError(nameof(filter.To), "'To' must be on or after 'From'.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _contracts.SearchAsync(filter, ct));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> GetById(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        return contract is null ? Problem(statusCode: 404, title: $"Contract {id} was not found.") : Ok(contract);
    }

    /// <summary>Creates a contract (always as Draft). The service level selects the creator in the contract factory.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractDto>> Create(CreateContractDto input, CancellationToken ct)
    {
        var result = await _contracts.CreateAsync(input, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ToProblem(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> Update(int id, UpdateContractDto input, CancellationToken ct)
    {
        var result = await _contracts.UpdateAsync(id, input, ct);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    /// <summary>Moves a contract to a new status. Only transitions allowed by the contract's current State succeed.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> ChangeStatus(int id, ChangeContractStatusDto input, CancellationToken ct)
    {
        var result = await _contracts.ChangeStatusAsync(id, input.Status, ct);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    /// <summary>Uploads the signed agreement (PDF only, max 10 MB). Replaces any previous file.</summary>
    [HttpPost("{id:int}/agreement")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PdfFileValidator.MaxFileSizeBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> UploadAgreement(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            ModelState.AddModelError(PdfFileValidator.FieldName, "Select a PDF file to upload.");
            return ValidationProblem(ModelState);
        }

        await using var stream = file.OpenReadStream();
        var result = await _contracts.UploadAgreementAsync(id, new FileUpload(file.FileName, file.ContentType, file.Length, stream), ct);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("{id:int}/agreement")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken ct)
    {
        var file = await _contracts.DownloadAgreementAsync(id, ct);
        return file is null
            ? Problem(statusCode: 404, title: $"No signed agreement found for contract {id}.")
            : File(file.Content, file.ContentType, file.FileName);
    }
}
