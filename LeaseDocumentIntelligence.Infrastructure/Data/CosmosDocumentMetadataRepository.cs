namespace LeaseDocumentIntelligence.Infrastructure.Data;

using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

/// <summary>
/// Cosmos DB implementation of document metadata repository
/// </summary>
public sealed class CosmosDocumentMetadataRepository : IDocumentMetadataRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;
    private readonly ILogger<CosmosDocumentMetadataRepository> _logger;
    private readonly CosmosDbOptions _options;

    public CosmosDocumentMetadataRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDocumentMetadataRepository> logger)
    {
        _cosmosClient = cosmosClient;
        _options = options.Value;
        _logger = logger;

        var database = _cosmosClient.GetDatabase(_options.DatabaseName);
        _container = database.GetContainer(_options.ContainerName);
    }

    public async Task<DocumentMetadata> CreateAsync(
        DocumentMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            metadata.Id = metadata.DocumentId.ToString();
            metadata.Pk = metadata.DocumentId.ToString();
            metadata.LastUpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Creating document {Id} with partition key {PK}", metadata.Id, metadata.Pk);
            var response = await _container.CreateItemAsync(
                metadata,
                new PartitionKey(metadata.Pk),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error creating document. Status: {Status}, SubStatus: {SubStatus}, Message: {Message}",
                ex.StatusCode, ex.SubStatusCode, ex.Message);
            throw;
        }
    }

    public async Task<DocumentMetadata?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // When we don't know the partition key, we need to query
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.documentId = @documentId")
                .WithParameter("@documentId", documentId);

            using var iterator = _container.GetItemQueryIterator<DocumentMetadata>(query);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                return response.FirstOrDefault();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document metadata not found for {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<IReadOnlyList<DocumentMetadata>> GetByUserAsync(
        string uploadedBy,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.uploadedBy = @uploadedBy ORDER BY c.uploadedAt DESC")
            .WithParameter("@uploadedBy", uploadedBy);

        var results = new List<DocumentMetadata>();

        using var iterator = _container.GetItemQueryIterator<DocumentMetadata>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(uploadedBy)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyList<DocumentMetadata>> GetByStatusAsync(
        DocumentStatus status,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.status = @status ORDER BY c._ts DESC")
            .WithParameter("@status", (int)status);

        var results = new List<DocumentMetadata>();

        using var iterator = _container.GetItemQueryIterator<DocumentMetadata>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<DocumentMetadata> UpdateAsync(
        DocumentMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        metadata.LastUpdatedAt = DateTime.UtcNow;
        var response = await _container.ReplaceItemAsync(
            metadata,
            metadata.Id,
            new PartitionKey(metadata.Pk),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task UpdateStatusAsync(
        Guid documentId,
        DocumentStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetByIdAsync(documentId, cancellationToken);
        if (metadata is null)
        {
            throw new InvalidOperationException(
                $"Document metadata not found for {documentId}");
        }

        metadata.Status = newStatus;
        metadata.LastUpdatedAt = DateTime.UtcNow;

        await _container.ReplaceItemAsync(
            metadata,
            metadata.Id,
            new PartitionKey(metadata.Pk),
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetByIdAsync(documentId, cancellationToken);
        if (metadata is null)
        {
            _logger.LogWarning(
                "Cannot delete: metadata not found for {DocumentId}",
                documentId);
            return;
        }

        await _container.DeleteItemAsync<DocumentMetadata>(
            metadata.Id,
            new PartitionKey(metadata.Pk),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentMetadata>> GetAllAsync(
        int pageSize = 100,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c ORDER BY c.uploadedAt DESC");

        var results = new List<DocumentMetadata>();

        using var iterator = _container.GetItemQueryIterator<DocumentMetadata>(
            query,
            continuationToken,
            new QueryRequestOptions { MaxItemCount = pageSize });

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }
}

/// <summary>
/// Configuration options for Cosmos DB
/// </summary>
public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    /// <summary>
    /// Cosmos DB account endpoint
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Database name
    /// </summary>
    public string DatabaseName { get; set; } = "LeaseDocuments";

    /// <summary>
    /// Container name for document metadata
    /// </summary>
    public string ContainerName { get; set; } = "Metadata";
}
