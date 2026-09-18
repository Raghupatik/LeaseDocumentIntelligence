namespace LeaseDocumentIntelligence.API.Controllers;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ReviewerOrAdmin")]
public class ReviewController : ControllerBase
{
    private readonly IReviewQueueService _reviewQueueService;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(
        IReviewQueueService reviewQueueService,
        ILogger<ReviewController> logger)
    {
        _reviewQueueService = reviewQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Get pending items for human review
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of items requiring review</returns>
    [HttpGet("pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReviewQueueItemDto>>> GetPendingReviews(
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await _reviewQueueService.GetReviewQueueAsync(cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending reviews");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving reviews");
        }
    }

    /// <summary>
    /// Approve a review item
    /// </summary>
    /// <param name="reviewItemId">Review item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{reviewItemId}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveReview(
        Guid reviewItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _reviewQueueService.ApproveReviewItemAsync(reviewItemId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving review item");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error approving review");
        }
    }

    /// <summary>
    /// Reject and correct a review item
    /// </summary>
    /// <param name="reviewItemId">Review item ID</param>
    /// <param name="request">Correction details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{reviewItemId}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectReview(
        Guid reviewItemId,
        [FromBody] ReviewCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CorrectedValue))
        {
            _logger.LogWarning("Reject review attempted with empty correction");
            return BadRequest("Corrected value is required");
        }

        try
        {
            await _reviewQueueService.RejectReviewItemAsync(
                reviewItemId,
                request.CorrectedValue,
                request.Notes ?? string.Empty,
                cancellationToken);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting review item");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error rejecting review");
        }
    }
}

public class ReviewCorrectionRequest
{
    public required string CorrectedValue { get; set; }
    public string? Notes { get; set; }
}
