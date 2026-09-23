namespace LeaseDocumentIntelligence.API.Controllers;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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

        var options = new LeaseSearchOptions
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

        var options = new LeaseSearchOptions
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

    /// <summary>
    /// Re-index all documents from Cosmos DB. Use when search results are stale.
    /// </summary>
    [HttpPost("reindex")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReindexAll(
        [FromServices] IDocumentMetadataRepository metadataRepo,
        CancellationToken cancellationToken)
    {
        var documents = await metadataRepo.GetAllAsync(1000, null, cancellationToken);
        var indexed = 0;
        var errors = new List<string>();

        foreach (var metadata in documents)
        {
            try
            {
                // Build a LeaseDocument from metadata for indexing
                var leaseDoc = new LeaseDocument
                {
                    Id = metadata.DocumentId,
                    FileName = metadata.FileName,
                    Status = metadata.Status,
                    UploadedAt = metadata.UploadedAt,
                    ExtractedFields = metadata.ExtractedFields ?? []
                };

                // Build document text from extracted fields for better search
                var documentText = string.Join("\n", 
                    metadata.ExtractedFields?.Select(f => $"{f.FieldName}: {f.ExtractedValue}") ?? []);

                await _searchService.IndexDocumentAsync(leaseDoc, documentText, cancellationToken);
                indexed++;
                _logger.LogInformation("Re-indexed document {DocumentId}", metadata.DocumentId);
            }
            catch (Exception ex)
            {
                errors.Add($"{metadata.DocumentId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to re-index document {DocumentId}", metadata.DocumentId);
            }
        }

        return Ok(new { 
            message = $"Re-indexed {indexed} documents", 
            total = documents.Count,
            errors = errors.Count > 0 ? errors : null 
        });
    }
}
