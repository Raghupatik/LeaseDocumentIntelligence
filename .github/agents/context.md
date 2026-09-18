# Lease Document Intelligence - Project Context

> **Purpose**: This document provides comprehensive context for engineers and AI agents to understand, extend, and maintain this project.

## Overview

**Lease Document Intelligence** is an enterprise .NET 10 application that extracts key terms from commercial lease PDFs using Azure AI Foundry. It automates the manual abstraction of 40-60 critical fields per lease (rent schedules, escalations, renewal options, CAM caps, etc.), reducing 2-4 hours of skilled labor per document while preventing revenue leakage from abstraction errors.

### Business Problem
- Commercial leases: 80-150 page bespoke legal documents
- Manual abstraction: 2-4 hours per document at $75/hour loaded cost
- Industry estimates: 1-2% of billable recoveries lost to abstraction errors
- Mid-size operator (500 leases, 20% annual turnover): ~$22,500/year abstraction labor + $100K-200K recovery leakage

---

## Architecture

### N-Tier Structure

```
LeaseDocumentIntelligence/
├── LeaseDocumentIntelligence/              # Web Project (Blazor UI + Startup)
│   ├── Components/                         # Blazor components and pages
│   │   ├── Pages/                          # Routable pages (DocumentUpload, ExtractionResults, etc.)
│   │   └── Layout/                         # Layout components
│   ├── Program.cs                          # Application entry point, DI configuration
│   ├── appsettings.json                    # Configuration (non-secrets)
│   └── Properties/launchSettings.json
│
├── LeaseDocumentIntelligence.API/          # API Controllers
│   └── Controllers/
│       ├── LeaseController.cs              # Document upload, extraction endpoints
│       └── ReviewController.cs             # Human review queue endpoints
│
├── LeaseDocumentIntelligence.Domain/       # Domain Layer (no dependencies)
│   ├── Models/                             # Entity models
│   │   ├── LeaseDocument.cs
│   │   ├── ExtractedField.cs
│   │   ├── ReviewQueueItem.cs
│   │   └── DocumentMetadata.cs
│   ├── DTOs/                               # Data transfer objects
│   │   ├── LeaseExtractionResultDto.cs
│   │   ├── ReviewQueueItemDto.cs
│   │   └── BatchUploadDto.cs
│   ├── Interfaces/                         # Service contracts (dependency inversion)
│   │   ├── ILeaseDocumentRepository.cs
│   │   ├── IDocumentProcessingService.cs
│   │   ├── IExtractionService.cs
│   │   ├── IFoundryAIService.cs
│   │   ├── IReviewQueueService.cs
│   │   ├── ISecretProvider.cs
│   │   ├── IBlobStorageService.cs
│   │   └── IDocumentMetadataRepository.cs
│   └── Constants/
│       └── AuthorizationConstants.cs
│
├── LeaseDocumentIntelligence.Infrastructure/  # Infrastructure Layer
│   ├── Services/
│   │   ├── FoundryAIService.cs             # Azure AI Foundry integration
│   │   ├── ExtractionService.cs            # Field extraction orchestration
│   │   ├── DocumentProcessingService.cs    # PDF text extraction (PdfPig)
│   │   ├── ReviewQueueService.cs           # Human review workflow
│   │   ├── BlobStorageService.cs           # Azure Blob Storage
│   │   └── KeyVaultSecretProvider.cs       # Azure Key Vault integration
│   ├── Data/
│   │   ├── LeaseDocumentRepository.cs      # In-memory repository
│   │   └── CosmosDocumentMetadataRepository.cs  # Cosmos DB for metadata
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs  # Clean DI registration
│
├── LeaseDocumentIntelligence.Tests/        # Unit Tests (xUnit)
│   └── Infrastructure/
│       ├── Services/
│       │   ├── ExtractionServiceTests.cs
│       │   ├── DocumentProcessingServiceTests.cs
│       │   └── ReviewQueueServiceTests.cs
│       └── Data/
│           └── LeaseDocumentRepositoryTests.cs
│
└── .github/
    ├── workflows/ci-cd.yml                 # CI/CD pipeline
    └── agents/context.md                   # This file
```

### Dependency Flow
```
Web → API → Infrastructure → Domain
         ↘              ↙
           Domain (interfaces)
```
- **Domain**: No external dependencies, defines contracts
- **Infrastructure**: Implements Domain interfaces, references Azure SDKs
- **API**: Controllers, references Domain + Infrastructure
- **Web**: Blazor UI, hosts everything, Program.cs configures DI

---

## Azure Services Integration

### Key Vault (Secrets Management)
- **Package**: `Azure.Extensions.AspNetCore.Configuration.Secrets`
- **Pattern**: Configuration provider loads secrets at startup
- **Naming**: Use `--` for hierarchy (e.g., `AzureAd--ClientSecret` → `AzureAd:ClientSecret`)
- **Caching**: `KeyVaultSecretProvider` caches secrets in `IMemoryCache`
- **Auth**: `DefaultAzureCredential` (Managed Identity in Azure, VS/CLI locally)

```csharp
// Program.cs - Key Vault as configuration source
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri) && !builder.Environment.IsDevelopment())
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}
```

### Blob Storage (Document Storage)
- **Package**: `Azure.Storage.Blobs`
- **Service**: `BlobStorageService` implements `IBlobStorageService`
- **Features**: Single upload, batch upload with `Parallel.ForEachAsync`
- **Naming**: `{uploadedBy}/{timestamp}-{documentId}-{filename}`
- **Auth**: `DefaultAzureCredential` (Managed Identity)

### Cosmos DB (Metadata Storage)
- **Package**: `Microsoft.Azure.Cosmos`
- **Service**: `CosmosDocumentMetadataRepository` implements `IDocumentMetadataRepository`
- **Pattern**: `CosmosClient` registered as **Singleton** (connection pooling)
- **Partition Key**: `uploadedBy` for user-based queries
- **Auth**: `DefaultAzureCredential`

### Azure AI Foundry (LLM Extraction)
- **Package**: `Azure.AI.OpenAI`
- **Service**: `FoundryAIService` implements `IFoundryAIService`
- **Pattern**: `IHttpClientFactory` with named client "FoundryAI"
- **Auth**: `DefaultAzureCredential` for token acquisition
- **Prompt**: Structured extraction with JSON schema, confidence scores, citations

### Application Insights (Logging/Monitoring)
- **Package**: `Microsoft.ApplicationInsights.AspNetCore`
- **Pattern**: Enabled only when valid connection string exists
- **Log Level**: Warning minimum (LogInformation calls removed)
- **Development**: Console logging only

---

## Authentication & Authorization

### Microsoft Entra ID (Azure AD)
- **Package**: `Microsoft.Identity.Web`
- **Flow**: OpenID Connect with authorization code
- **Scopes**: openid, profile, offline_access

### Role-Based Authorization
```csharp
// Policies defined in Program.cs
.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
.AddPolicy("LeaseAnalystOrAdmin", policy => policy.RequireRole("Admin", "LeaseAnalyst"))
.AddPolicy("ReviewerOrAdmin", policy => policy.RequireRole("Admin", "Reviewer"))
.AddPolicy("AllAuthenticatedUsers", policy => policy.RequireAuthenticatedUser())
```

### Roles
| Role | Permissions |
|------|-------------|
| Admin | Full access, user management |
| LeaseAnalyst | Upload documents, extract fields |
| Reviewer | Review and approve extracted fields |
| Viewer | Read-only access to results |

---

## Coding Standards

### C# Conventions (Microsoft Guidelines)
- Async/await throughout with `CancellationToken`
- `IHttpClientFactory` for HTTP calls (never `new HttpClient()`)
- `ILogger<T>` with structured logging (only Warning/Error levels)
- Nullable reference types enabled
- File-scoped namespaces
- Primary constructors where appropriate

### SOLID Principles
- **S**: Single responsibility per class
- **O**: Open for extension (interfaces in Domain)
- **L**: Liskov substitution (interface implementations)
- **I**: Interface segregation (focused interfaces)
- **D**: Dependency inversion (Domain defines contracts)

### Repository Pattern
- Interfaces in `Domain/Interfaces/`
- Implementations in `Infrastructure/Data/`
- Async methods with `CancellationToken`

### Error Handling
- `try/catch` with `_logger.LogError(ex, "Message")`
- Throw exceptions up to controller for HTTP response mapping
- Validation errors return 400 Bad Request

---

## API Endpoints

### Lease Controller (`/api/lease`)
| Method | Endpoint | Description | Auth Policy |
|--------|----------|-------------|-------------|
| POST | `/upload` | Upload single PDF | AllAuthenticatedUsers |
| POST | `/upload/batch` | Batch upload with concurrency | AllAuthenticatedUsers |
| POST | `/{id}/extract` | Extract fields from document | AllAuthenticatedUsers |
| GET | `/{id}/result` | Get extraction results | AllAuthenticatedUsers |

### Review Controller (`/api/review`)
| Method | Endpoint | Description | Auth Policy |
|--------|----------|-------------|-------------|
| GET | `/queue` | Get pending review items | ReviewerOrAdmin |
| POST | `/{id}/approve` | Approve extracted field | ReviewerOrAdmin |
| POST | `/{id}/reject` | Reject with correction | ReviewerOrAdmin |

### OpenAPI/Scalar
- **Scalar UI**: `/scalar/v1`
- **OpenAPI JSON**: `/openapi/v1.json`

---

## Configuration (appsettings.json)

```json
{
  "KeyVault": {
    "VaultUri": "https://<keyvault>.vault.azure.net/"
  },
  "BlobStorage": {
    "StorageAccountUri": "https://<storage>.blob.core.windows.net/",
    "ContainerName": "lease-documents"
  },
  "CosmosDb": {
    "Endpoint": "https://<cosmos>.documents.azure.com:443/",
    "DatabaseName": "LeaseDocuments",
    "ContainerName": "Metadata"
  },
  "AzureAd": {
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "ClientSecret": "[FROM-KEYVAULT]"
  },
  "Foundry": {
    "Endpoint": "https://<foundry>.openai.azure.com/",
    "ModelName": "gpt-4"
  },
  "ApplicationInsights": {
    "ConnectionString": "[FROM-KEYVAULT]"
  }
}
```

### Key Vault Secrets Required
- `AzureAd--ClientSecret`
- `ApplicationInsights--ConnectionString`

---

## Testing

### Framework
- **xUnit** for unit tests
- **Moq** for mocking
- **FluentAssertions** for assertions

### Test Structure
```
LeaseDocumentIntelligence.Tests/
└── Infrastructure/
    ├── Services/       # Service unit tests
    └── Data/           # Repository unit tests
```

### Running Tests
```bash
dotnet test --verbosity normal
```

---

## CI/CD Pipeline

### Workflow: `.github/workflows/ci-cd.yml`

1. **Build & Test** (on all branches)
   - Restore, build, run unit tests
   - Upload test results and coverage

2. **Publish** (main branch only)
   - Create release build
   - Upload artifact

3. **Deploy** (main branch, requires approval)
   - Azure login with Managed Identity (OIDC)
   - Deploy to Azure App Service

### Required GitHub Secrets
- `AZURE_CLIENT_ID`: App registration client ID
- `AZURE_TENANT_ID`: Azure AD tenant ID
- `AZURE_SUBSCRIPTION_ID`: Azure subscription ID

---

## Local Development

### Prerequisites
- .NET 10 SDK
- Visual Studio 2026 or VS Code
- Azure CLI (for DefaultAzureCredential)

### Running Locally
```bash
cd LeaseDocumentIntelligence
dotnet run --urls "https://localhost:7231"
```

### Development Mode Differences
- Key Vault configuration provider disabled
- Console logging enabled
- Application Insights skipped if placeholder connection string

---

## Extending the Project

### Adding a New Extracted Field
1. Add field to `ExtractedField.cs` or update extraction prompt
2. Update `FoundryAIService.cs` prompt schema
3. Add confidence calculation logic
4. Update UI in `ExtractionResults.razor`

### Adding a New Service
1. Define interface in `Domain/Interfaces/`
2. Implement in `Infrastructure/Services/`
3. Register in `ServiceCollectionExtensions.AddInfrastructureServices()`
4. Inject via constructor

### Adding a New API Endpoint
1. Add method to existing controller or create new controller in `API/Controllers/`
2. Apply appropriate `[Authorize(Policy = "...")]` attribute
3. Document with XML comments for OpenAPI

---

## Package Dependencies (Key)

| Package | Version | Purpose |
|---------|---------|---------|
| Azure.Identity | 1.21.0 | DefaultAzureCredential |
| Azure.AI.OpenAI | 2.0.0 | AI Foundry integration |
| Azure.Storage.Blobs | 12.29.2 | Blob storage |
| Microsoft.Azure.Cosmos | 3.63.1 | Cosmos DB |
| Azure.Security.KeyVault.Secrets | 4.11.1 | Key Vault |
| Microsoft.Identity.Web | 2.16.0 | Entra ID auth |
| PdfPig | 0.1.16 | PDF text extraction |
| Scalar.AspNetCore | 2.17.4 | API documentation UI |
| Newtonsoft.Json | 13.0.3 | JSON (Cosmos requirement) |

---

## Security Considerations

- All secrets in Azure Key Vault (never in appsettings)
- Managed Identity for Azure service authentication
- HTTPS enforced
- Role-based authorization on all endpoints
- Input validation on file uploads
- No vulnerable package dependencies

---

## Contact & Support

For questions about extending this project, review this context document first, then consult the codebase directly. Key files to understand:
- `Program.cs` - DI setup, middleware configuration
- `ServiceCollectionExtensions.cs` - Infrastructure service registration
- `FoundryAIService.cs` - AI extraction logic
- `LeaseController.cs` - Main API endpoints
