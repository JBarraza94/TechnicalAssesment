using CompanyApi.Api.Contracts;
using CompanyApi.Api.Mapping;
using CompanyApi.Application.Common;
using CompanyApi.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyApi.Api.Controllers;

/// <summary>Create, list/search and fetch companies. Every endpoint requires a bearer token.</summary>
[ApiController]
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[Route("api/companies")]
[Produces("application/json")]
public sealed class CompaniesController(ICompanyService companyService) : ControllerBase
{
    /// <summary>Creates a company after validating the name, the URL and their relevance to each other.</summary>
    /// <response code="201">Company created; the Location header points to it.</response>
    /// <response code="400">Validation failed (name, URL, relevance or malformed body).</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var result = await companyService.CreateAsync(request.ToCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value.ToResponse())
            : ToError(result);
    }

    /// <summary>Lists companies, optionally filtered by name/domain or ranked by a relevance query.</summary>
    /// <remarks>
    /// Returns a JSON array (empty when nothing matches). When pageSize is supplied, the X-Total-Count header
    /// carries the total number of matches.
    /// </remarks>
    /// <response code="200">Matching companies (possibly empty).</response>
    /// <response code="400">Invalid query parameters.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanySearchResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CompanySearchResult>>> Search(
        [FromQuery] CompanySearchRequest query, CancellationToken cancellationToken)
    {
        var results = await companyService.SearchAsync(query.ToQuery(), cancellationToken);
        Response.Headers["X-Total-Count"] = results.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Ok(results.Items.Select(h => h.ToSearchResult()).ToList());
    }

    /// <summary>Gets a company by id.</summary>
    /// <param name="id">Company GUID. A value that is not a GUID returns 400.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The company.</response>
    /// <response code="400">The id is not a valid GUID.</response>
    /// <response code="404">No company has that id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var companyId))
        {
            return BadRequest(ErrorResponse.From($"'{id}' is not a valid company id (expected a GUID)."));
        }

        var result = await companyService.GetByIdAsync(companyId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value!.ToResponse()) : ToError(result);
    }

    private ObjectResult ToError<T>(Result<T> result)
    {
        var body = new ErrorResponse(result.Message ?? "The request could not be processed.", result.Errors);
        return result.Status switch
        {
            ResultStatus.NotFound => NotFound(body),
            _ => BadRequest(body),
        };
    }
}
