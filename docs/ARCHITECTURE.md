# Lease Document Intelligence - Architecture

## Overview
A .NET-based intelligent lease document extraction system using Azure AI Foundry models with human-in-the-loop review workflow. This document covers the complete Azure infrastructure topology and application architecture.

---

## Azure Infrastructure Architecture

```mermaid
flowchart TB
    subgraph Internet["🌐 Internet"]
        User["👤 User"]
    end

    subgraph DNS["DNS & Traffic Management"]
        style DNS fill:#E6F3FF,stroke:#0078D4
        AzureDNS["🌐 Azure DNS Zone"]
        TrafficMgr["🔀 Traffic Manager Profile"]
    end

    subgraph EdgeServices["Edge & Security Layer"]
        style EdgeServices fill:#FFF4E6,stroke:#FF8C00
        FrontDoor["🚪 Azure Front Door<br/><i>Priority 1 - Primary</i>"]
        AppGW["🛡️ Application Gateway<br/><i>Priority 2 - Failover</i>"]
    end

    subgraph WebTier["Presentation Tier"]
        style WebTier fill:#E8F5E9,stroke:#4CAF50
        WebApp["🖥️ Azure Web App<br/><i>Blazor Server UI</i>"]
    end

    subgraph Identity["Identity & Secrets"]
        style Identity fill:#F3E5F5,stroke:#9C27B0
        EntraID["🔐 Microsoft Entra ID<br/><i>Authentication</i>"]
        KeyVault["🔑 Azure Key Vault<br/><i>Secrets & Certificates</i>"]
    end

    subgraph DataTier["Data Tier"]
        style DataTier fill:#E3F2FD,stroke:#2196F3
        Storage["📦 Azure Storage Account<br/><i>Blob: Lease PDFs</i>"]
        CosmosDB["🗄️ Azure Cosmos DB<br/><i>Document Metadata</i>"]
    end

    subgraph AsyncProcessing["Async Processing"]
        style AsyncProcessing fill:#FFF3E0,stroke:#FF9800
        ServiceBus["📬 Azure Service Bus<br/><i>Document Queue</i>"]
        FuncApp["⚙️ Azure Function App<br/><i>Service Bus Trigger</i>"]
    end

    subgraph AIServices["AI Services"]
        style AIServices fill:#FCE4EC,stroke:#E91E63
        AIFoundry["🤖 Azure AI Foundry<br/><i>OpenAI GPT Models</i>"]
    end

    subgraph APIManagement["API Management"]
        style APIManagement fill:#E8EAF6,stroke:#3F51B5
        APIM["🔌 Azure API Management<br/><i>AI Gateway + Rate Limiting</i>"]
    end

    %% User Flow
    User --> AzureDNS
    AzureDNS --> TrafficMgr
    TrafficMgr -->|Priority 1| FrontDoor
    TrafficMgr -->|Priority 2| AppGW
    FrontDoor --> WebApp
    AppGW --> WebApp

    %% Web App Integrations
    WebApp <-->|"OAuth 2.0"| EntraID
    WebApp -->|"Managed Identity"| KeyVault
    WebApp -->|"Upload PDFs"| Storage
    WebApp -->|"Store Metadata"| CosmosDB
    WebApp -->|"Call APIs"| APIM

    %% Async Processing Flow
    WebApp -->|"1. Upload PDF"| Storage
    WebApp -->|"2. Create Metadata"| CosmosDB
    WebApp -->|"3. Queue Message"| ServiceBus
    ServiceBus -->|"4. Trigger"| FuncApp
    FuncApp -->|"5. Extract Terms"| AIFoundry
    FuncApp -->|"6. Update Status<br/>+ Fields Count"| CosmosDB

    %% Function App Integrations
    FuncApp -->|"Managed Identity"| KeyVault
    FuncApp -->|"Read PDFs"| Storage

    %% APIM Flow
    APIM -->|"Route"| FuncApp
    APIM -->|"Managed Identity"| AIFoundry

    %% Styling
    classDef azure fill:#0078D4,stroke:#005A9E,color:#fff
    classDef security fill:#9C27B0,stroke:#7B1FA2,color:#fff
    classDef data fill:#2196F3,stroke:#1976D2,color:#fff
    classDef ai fill:#E91E63,stroke:#C2185B,color:#fff
```

---

## Request Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    participant U as 👤 User
    participant DNS as 🌐 DNS Zone
    participant TM as 🔀 Traffic Manager
    participant FD as 🚪 Front Door
    participant WA as 🖥️ Web App
    participant EID as 🔐 Entra ID
    participant KV as 🔑 Key Vault
    participant SA as 📦 Storage
    participant CDB as 🗄️ Cosmos DB
    participant SB as 📬 Service Bus
    participant FA as ⚙️ Function App
    participant AI as 🤖 AI Foundry

    rect rgb(230, 243, 255)
        Note over U,FD: DNS Resolution & Edge Routing
        U->>DNS: Resolve domain
        DNS->>TM: Route request
        TM->>FD: Priority 1 routing
        FD->>WA: Forward to Web App
    end

    rect rgb(243, 229, 245)
        Note over WA,EID: Authentication Flow
        WA->>EID: Redirect to login
        EID->>U: Present login page
        U->>EID: Authenticate
        EID->>WA: Return tokens + cookies
    end

    rect rgb(232, 245, 233)
        Note over WA,SB: Document Upload & Queue Flow
        WA->>KV: Get connection strings (MI)
        WA->>SA: 1. Upload lease PDF (MI)
        WA->>CDB: 2. Create metadata (status: Pending)
        WA->>SB: 3. Publish message to queue
    end

    rect rgb(255, 243, 224)
        Note over SB,AI: Async Processing (Service Bus Trigger)
        SB->>FA: Trigger on message
        FA->>KV: Get AI credentials (MI)
        FA->>SA: Download PDF (MI)
        FA->>AI: Extract lease terms
        AI->>FA: Return extracted fields
        FA->>CDB: Update status: Complete + fieldsCount + reviewCount
    end

    rect rgb(232, 234, 246)
        Note over U,FA: API Access via APIM
        U->>WA: Request extraction results
        WA->>APIM: GET /api/lease/{id}/results
        APIM->>FA: Route to Function API
        FA->>CDB: Query results
        FA->>APIM: Return JSON
        APIM->>WA: Return response
        WA->>U: Display results
    end
```

---

## Component Responsibilities

| Component | Responsibility | Authentication |
|-----------|---------------|----------------|
| **Azure DNS Zone** | Domain name resolution, record management | N/A |
| **Traffic Manager** | Geographic routing, failover between regions | N/A |
| **Front Door** | Global load balancing, WAF, SSL termination, CDN | Managed Identity |
| **Application Gateway** | Regional load balancing, WAF, SSL offloading | Managed Identity |
| **Web App (Blazor)** | UI rendering, user interaction, file upload | Entra ID OAuth 2.0 |
| **Entra ID** | User authentication, role-based authorization | N/A |
| **Key Vault** | Secrets, certificates, connection strings | Managed Identity |
| **Storage Account** | Blob storage for lease PDFs | Managed Identity |
| **Cosmos DB** | Document metadata, extraction results, status tracking | Managed Identity |
| **Service Bus** | Async message queue for document processing | Managed Identity |
| **Function App** | Service Bus trigger, AI orchestration, APIs | Managed Identity |
| **AI Foundry (OpenAI)** | LLM for lease term extraction | Managed Identity via APIM |
| **API Management** | API gateway, rate limiting, AI gateway policies | Managed Identity |

---

## Managed Identity Flow

```mermaid
flowchart LR
    subgraph Resources["Azure Resources"]
        WA["🖥️ Web App"]
        FA["⚙️ Function App"]
        APIM["🔌 APIM"]
    end

    subgraph ManagedIdentity["🔐 Managed Identities"]
        WA_MI["Web App MI"]
        FA_MI["Function MI"]
        APIM_MI["APIM MI"]
    end

    subgraph TargetServices["Target Services"]
        KV["🔑 Key Vault"]
        SA["📦 Storage"]
        CDB["🗄️ Cosmos DB"]
        SB["📬 Service Bus"]
        AI["🤖 AI Foundry"]
    end

    WA --> WA_MI
    FA --> FA_MI
    APIM --> APIM_MI

    WA_MI -->|"Get Secrets"| KV
    WA_MI -->|"Upload Blobs"| SA
    WA_MI -->|"Read/Write"| CDB
    WA_MI -->|"Send Messages"| SB

    FA_MI -->|"Get Secrets"| KV
    FA_MI -->|"Read Blobs"| SA
    FA_MI -->|"Update Status"| CDB
    FA_MI -->|"Receive Messages"| SB

    APIM_MI -->|"Call Models"| AI

    style ManagedIdentity fill:#F3E5F5,stroke:#9C27B0
    style TargetServices fill:#E3F2FD,stroke:#2196F3
```

---

## N-Tier Application Layers

```mermaid
flowchart TB
    subgraph Presentation["🖥️ Presentation Layer"]
        style Presentation fill:#E8F5E9,stroke:#4CAF50
        Blazor["Blazor Server Components"]
        Home["Home.razor"]
        Upload["DocumentUpload.razor"]
        Results["ExtractionResults.razor"]
        Review["ReviewQueue.razor"]
    end

    subgraph APILayer["🔌 API Layer"]
        style APILayer fill:#E3F2FD,stroke:#2196F3
        LeaseCtrl["LeaseController"]
        ReviewCtrl["ReviewController"]
    end

    subgraph BusinessLogic["⚙️ Business Logic Layer"]
        style BusinessLogic fill:#FFF3E0,stroke:#FF9800
        DocSvc["DocumentProcessingService"]
        AISvc["FoundryAIService"]
        ExtSvc["ExtractionService"]
        RevSvc["ReviewQueueService"]
    end

    subgraph DataAccess["🗄️ Data Access Layer"]
        style DataAccess fill:#FCE4EC,stroke:#E91E63
        LeaseRepo["LeaseDocumentRepository"]
        BlobSvc["BlobStorageService"]
    end

    subgraph External["☁️ External Services"]
        style External fill:#F3E5F5,stroke:#9C27B0
        CosmosDB["Cosmos DB"]
        Storage["Blob Storage"]
        AIFoundry["AI Foundry"]
    end

    Blazor --> Home & Upload & Results & Review
    Home & Upload & Results & Review --> LeaseCtrl & ReviewCtrl
    LeaseCtrl & ReviewCtrl --> ExtSvc & RevSvc
    ExtSvc --> DocSvc & AISvc
    DocSvc & AISvc & ExtSvc & RevSvc --> LeaseRepo & BlobSvc
    LeaseRepo --> CosmosDB
    BlobSvc --> Storage
    AISvc --> AIFoundry
```

### 1. Presentation Layer
**Location**: `LeaseDocumentIntelligence/Components/`
- **Blazor Components** (.razor files)
  - `Pages/Home.razor` - Dashboard & landing page
  - `Pages/DocumentUpload.razor` - File upload interface
  - `Pages/ExtractionResults.razor` - Results display with citations
  - `Pages/ReviewQueue.razor` - Human review interface
- **Responsibilities**: User interaction, file upload handling, results visualization

### 2. API Layer
**Location**: `LeaseDocumentIntelligence.API/Controllers/`
- **LeaseController.cs**
  - `POST /api/lease/upload` - Upload document
  - `GET /api/lease/{id}/results` - Get extraction results
  - `GET /api/lease/{id}/status` - Get document status
  - `GET /api/lease` - List all documents
- **ReviewController.cs**
  - `GET /api/review/pending` - Get pending review items
  - `POST /api/review/{id}/approve` - Approve extraction
  - `POST /api/review/{id}/reject` - Correct and reject

### 3. Business Logic Layer
**Location**: `LeaseDocumentIntelligence.Infrastructure/Services/`

#### Service Interfaces
- **IDocumentProcessingService** - PDF text extraction & validation
- **IFoundryAIService** - LLM calls with Managed Identity
- **IExtractionService** - Orchestrates extraction workflow
- **IReviewQueueService** - Manages low-confidence field routing

#### Implementations
- **DocumentProcessingService**
  - Uses PdfPig (UglyToad.PdfPig) for PDF parsing
  - Validates file format & size
  - Extracts text per page

- **FoundryAIService**
  - Calls Azure Foundry Models via HttpClientFactory
  - Uses DefaultAzureCredential (Managed Identity)
  - Prompts: extraction & validation
  - Parses JSON responses
  - Confidence scoring & citation extraction

- **ExtractionService**
  - Coordinates full extraction pipeline
  - Routes low-confidence items to review queue
  - Updates document status
  - Builds summary statistics

- **ReviewQueueService**
  - Manages review queue (in-memory for MVP)
  - Approval/rejection workflow
  - Tracks reviewer & timestamp

### 4. Data Access Layer
**Location**: `LeaseDocumentIntelligence/Infrastructure/Data/`
- **ILeaseDocumentRepository** - Data access interface
- **LeaseDocumentRepository** - In-memory implementation (extensible to EF Core/SQL)
  - CRUD operations
  - Document lifecycle management

### 5. Domain Layer
**Location**: `LeaseDocumentIntelligence/Domain/`

#### Models
- **LeaseDocument** - Document metadata & lifecycle
- **ExtractedField** - Field with confidence, citations, source
- **ReviewQueueItem** - Low-confidence item for human review

#### DTOs
- **ExtractionResultDto** - Results response
- **ExtractedFieldDto** - Field response
- **ReviewQueueItemDto** - Review item response

#### Constants
- **LeaseFieldDefinitions** - 40-60 predefined fields with metadata

## Key Architecture Decisions

### Async-First with HttpClientFactory
```csharp
services.AddHttpClient<IFoundryAIService, FoundryAIService>();
```
- All service methods are async
- HttpClientFactory manages connection pooling
- Scalable for high throughput

### Managed Identity for Azure Auth
```csharp
using var tokenProvider = new DefaultAzureCredential();
var token = await tokenProvider.GetTokenAsync(...);
```
- No credential strings in config
- Works in local dev (VS, CLI) and production (App Service, AKS)
- Azure RBAC for fine-grained permissions

### Confidence-Based Triage
- **≥70% confidence**: Approved automatically
- **<70% confidence**: Routed to review queue
- Reviewer can approve or correct with notes
- Maintains audit trail

### Separation of Concerns
- Services are testable (mocked in unit tests)
- DTOs decouple API contracts from domain models
- Repository pattern isolates data access (easy to swap in SQL)

## Scalability Considerations

### Current (MVP)
- In-memory data store
- Single document processing at a time
- Synchronous review workflow

### Future Enhancements
- **Phase 2**: Azure Service Bus, Function App triggers, APIM AI Gateway, batch uploads, Redis caching
- **Phase 3**: Multi-region deployment, Azure AI Search, SignalR notifications

### Reducing Human-in-the-Loop (Phase 3+)

The current architecture routes low-confidence extractions (<70%) to human review. To minimize manual intervention over time:

| Strategy | Implementation | Benefit |
|----------|---------------|---------|
| **Prompt Flow Evals** | Create evaluation datasets from reviewed extractions | Measure accuracy, identify prompt improvements |
| **Ground Truth Dataset** | Build labeled dataset from human-approved extractions | Benchmark model performance objectively |
| **Confidence Calibration** | Analyze reviewed items to tune thresholds | Reduce false positives in review queue |
| **Fine-Tuning** | Train on domain-specific lease terminology | Improve extraction accuracy for edge cases |
| **Active Learning** | Prioritize uncertain items for review | Maximize learning from minimal human effort |
| **Prompt Versioning** | A/B test prompts via Prompt Flow experiments | Data-driven prompt optimization |
| **Automated Regression** | Run eval suite before deployment | Prevent quality degradation |

**Goal**: Progressively reduce human review to only true edge cases (ambiguous clauses, non-standard formats).

## Architecture Pillars

This architecture is designed around the **Azure Well-Architected Framework** pillars:

### 🔐 Security
| Feature | Implementation |
|---------|---------------|
| **User Authentication** | Microsoft Entra ID (OAuth 2.0 / OIDC) |
| **Authorization** | Policy-based RBAC (Admin, Reviewer, User roles) |
| **Service-to-Service Auth** | Managed Identity (zero secrets in code) |
| **Secrets Management** | Azure Key Vault as configuration provider |
| **Network Security** | Private Endpoints, NSG, VNET integration |
| **Edge Protection** | WAF on Front Door, DDoS Protection Standard |
| **Data Encryption** | TLS 1.3 in-transit, CMK at-rest |

### 🔄 Disaster Recovery (DR)
| Feature | Implementation |
|---------|---------------|
| **Multi-Region Database** | Cosmos DB with multi-region writes |
| **Global Traffic Routing** | Traffic Manager with priority-based failover |
| **Geo-Redundant Storage** | GRS for blob storage (lease PDFs) |
| **Regional Failover** | Front Door (Priority 1) → App Gateway (Priority 2) |
| **RPO/RTO** | Near-zero RPO with Cosmos, minutes RTO with Traffic Manager |

### ⚡ High Availability (HA)
| Feature | Implementation |
|---------|---------------|
| **Load Balancing** | Azure Front Door (global), App Gateway (regional) |
| **Auto-Failover** | Traffic Manager health probes with automatic routing |
| **Zone Redundancy** | App Service, Functions, Cosmos DB zone-redundant |
| **Service Bus** | Premium tier with availability zones |
| **Health Monitoring** | Application Insights with alerting |

### 📈 Scalability
| Feature | Implementation |
|---------|---------------|
| **Async Processing** | Service Bus queue decouples upload from extraction |
| **Serverless Compute** | Function App auto-scales on demand |
| **Concurrent Processing** | Multiple Function instances process queue in parallel |
| **Connection Pooling** | HttpClientFactory for efficient HTTP connections |
| **Cosmos DB** | Serverless/autoscale with unlimited throughput |
| **CDN** | Front Door caches static content at edge |

### 🛡️ Resiliency
| Feature | Implementation |
|---------|---------------|
| **Message Durability** | Service Bus guarantees at-least-once delivery |
| **Dead-Letter Queue** | Failed messages preserved for analysis |
| **Retry Policies** | Exponential backoff on transient failures |
| **Circuit Breaker** | APIM policies prevent cascade failures |
| **Graceful Degradation** | Low-confidence items queue for human review |
| **Idempotent Operations** | Safe to retry extraction operations |

### 📊 Observability
| Feature | Implementation |
|---------|---------------|
| **Distributed Tracing** | Application Insights end-to-end correlation |
| **Structured Logging** | ILogger with App Insights sink |
| **Log Levels** | Warning/Error only (no verbose logs in production) |
| **Metrics** | Request rates, latencies, error rates |
| **Alerting** | Smart detection + custom alerts |
| **Dashboards** | Azure Monitor workbooks |

---

## Security Architecture

```mermaid
flowchart TB
    subgraph SecurityLayers["🔒 Security Layers"]
        direction TB
        
        subgraph Edge["Edge Security"]
            style Edge fill:#FFEBEE,stroke:#F44336
            WAF["🛡️ WAF on Front Door<br/><i>OWASP Rules</i>"]
            DDoS["🚫 DDoS Protection<br/><i>Standard Tier</i>"]
        end

        subgraph Network["Network Security"]
            style Network fill:#E3F2FD,stroke:#2196F3
            VNET["🌐 Virtual Network"]
            NSG["🔐 Network Security Groups"]
            PEP["🔗 Private Endpoints"]
        end

        subgraph Identity["Identity Security"]
            style Identity fill:#F3E5F5,stroke:#9C27B0
            EntraID["👤 Entra ID<br/><i>OAuth 2.0 / OIDC</i>"]
            RBAC["📋 Role-Based Access<br/><i>Admin, Reviewer, User</i>"]
            MI["🆔 Managed Identity<br/><i>Zero Secrets</i>"]
        end

        subgraph Data["Data Security"]
            style Data fill:#E8F5E9,stroke:#4CAF50
            TLS["🔒 TLS 1.3<br/><i>In Transit</i>"]
            Encryption["🔑 CMK Encryption<br/><i>At Rest</i>"]
            KV["🗝️ Key Vault<br/><i>Secrets Management</i>"]
        end
    end

    WAF --> DDoS
    DDoS --> VNET
    VNET --> NSG
    NSG --> PEP
    PEP --> EntraID
    EntraID --> RBAC
    RBAC --> MI
    MI --> TLS
    TLS --> Encryption
    Encryption --> KV
```

### Authentication & Authorization
- **Entra ID** for user authentication (OAuth 2.0 / OpenID Connect)
- **Role-Based Access Control** with policies:
  - `AdminOnly` - Full access to all operations
  - `ReviewerOrAdmin` - Access to review queue
  - `AllUsers` - Basic document operations
- **Managed Identity** for all service-to-service authentication

### Zero-Trust Secrets Management
- No secrets in `appsettings.json` (placeholders only)
- **Azure Key Vault** as configuration provider in production
- All Azure resources accessed via Managed Identity
- Key Vault secret naming: `AzureAd--ClientSecret` → `AzureAd:ClientSecret`

### Network Security
- Private endpoints for Storage, Cosmos DB, Key Vault
- NSG rules restrict inbound traffic
- VNET integration for Web App and Functions

### Data Protection
- TLS 1.3 enforced for all connections
- Customer-Managed Keys (CMK) for encryption at rest
- Blob soft-delete and versioning enabled

## Testing

### Unit Test Project
- **LeaseDocumentIntelligence.Tests**
- xUnit framework
- Moq for dependency injection
- FluentAssertions for readable tests

**Test Coverage**:
- DocumentProcessingService - validation & extraction
- ExtractionService - orchestration & error handling
- ReviewQueueService - queue management
- LeaseDocumentRepository - CRUD operations

## Deployment Architecture

```mermaid
flowchart LR
    subgraph Dev["👨‍💻 Development"]
        Code["Source Code"]
        PR["Pull Request"]
    end

    subgraph CI["🔄 CI Pipeline"]
        Build["Build & Test"]
        Publish["Publish Artifact"]
    end

    subgraph CD["🚀 CD Pipeline"]
        AzLogin["Azure Login (OIDC)"]
        Deploy["Deploy to App Service"]
    end

    subgraph Azure["☁️ Azure Production"]
        AppService["Web App"]
        FuncApp["Function App"]
    end

    Code --> PR
    PR --> Build
    Build --> Publish
    Publish --> AzLogin
    AzLogin --> Deploy
    Deploy --> AppService
    Deploy --> FuncApp

    style Dev fill:#E3F2FD,stroke:#2196F3
    style CI fill:#FFF3E0,stroke:#FF9800
    style CD fill:#E8F5E9,stroke:#4CAF50
    style Azure fill:#F3E5F5,stroke:#9C27B0
```

### CI/CD Pipeline (GitHub Actions)
- **Build**: Restore, build, test all projects
- **Publish**: Create deployment artifact
- **Deploy**: Azure login via OIDC (Managed Identity), publish to App Service

### Local Development
1. Clone repo
2. Create `appsettings.Development.json` with Foundry endpoint
3. `dotnet run`
4. Navigate to https://localhost:7231

### Azure Resources Required
| Resource | Purpose | SKU |
|----------|---------|-----|
| App Service Plan | Host Web App | P1v3 |
| Web App | Blazor Server UI | .NET 10 |
| Function App | Document processing APIs | Consumption |
| Storage Account | Lease PDF blobs | Standard_GRS |
| Cosmos DB | Document metadata | Serverless |
| Key Vault | Secrets & certificates | Standard |
| API Management | AI Gateway, rate limiting | Developer |
| Application Insights | Telemetry & logging | Basic |
| Front Door | Global load balancing | Standard |
| Application Gateway | Regional LB, WAF | WAF_v2 |

## Configuration

### appsettings.json (Development)
```json
{
  "Foundry": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "ModelName": "gpt-4o",
    "ApiVersion": "2024-02-15-preview"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>"
  },
  "KeyVault": {
    "Uri": "https://<keyvault-name>.vault.azure.net/"
  }
}
```

### Key Vault Secrets
| Secret Name | Maps To |
|-------------|---------|
| `AzureAd--ClientSecret` | `AzureAd:ClientSecret` |
| `CosmosDb--ConnectionString` | `CosmosDb:ConnectionString` |
| `Storage--ConnectionString` | `Storage:ConnectionString` |
| `ApplicationInsights--ConnectionString` | App Insights key |

## API Documentation

- **OpenAPI**: `/openapi/v1.json`
- **Scalar UI**: `/scalar/v1` (development only)

All endpoints documented with XML comments and OpenAPI metadata.
