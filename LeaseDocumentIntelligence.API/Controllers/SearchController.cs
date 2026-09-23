namespace LeaseDocumentIntelligence.API.Controllers;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// API endpoints for semantic search over lease documents.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IVectorSearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IVectorSearchService searchService,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Search lease documents using natural language with AI-powered reasoning.
    /// </summary>
    /// <param name="request">Search query and options</param>
    /// <returns>Search results with AI-generated insights</returns>
    [HttpPost]
    [ProducesResponseType<ReasonedSearchResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReasonedSearchResultDto>> Search(
        [FromBody] SearchRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query cannot be empty");
        }

        _logger.LogInformation("Searching with query: {Query}", request.Query);

        var options = new SearchOptions
        {
            Top = request.Top,
            TenantFilter = request.TenantFilter,
            UseHybridSearch = true
        };

        if (request.IncludeReasoning)
        {
            var result = await _searchService.SearchWithReasoningAsync(
                request.Query, options, cancellationToken);
            return Ok(result);
        }
        else
        {
            var result = await _searchService.SearchAsync(
                request.Query, options, cancellationToken);
            return Ok(new ReasonedSearchResultDto
            {
                Query = request.Query,
                SearchResults = result
            });
        }
    }

    /// <summary>
    /// Quick search without AI reasoning (faster response).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<SearchResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchResultDto>> QuickSearch(
        [FromQuery] string q,
        [FromQuery] int top = 10,
        [FromQuery] string? tenant = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Query parameter 'q' is required");
        }

        var options = new SearchOptions
        {
            Top = top,
            TenantFilter = tenant,
            UseHybridSearch = true
        };

        var result = await _searchService.SearchAsync(q, options, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Initialize the search index (admin only).
    /// </summary>
    [HttpPost("initialize")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InitializeIndex(CancellationToken cancellationToken)
    {
        await _searchService.EnsureIndexExistsAsync(cancellationToken);
        return Ok(new { message = "Search index initialized successfully" });
    }
}
