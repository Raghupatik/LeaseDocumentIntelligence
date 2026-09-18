# Lease Document Intelligence

A cutting-edge .NET-based AI system for intelligent extraction of critical lease terms from commercial documents with human-in-the-loop review and confidence-based quality assurance.



> 📐 **For detailed architecture diagrams and infrastructure topology, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**
>
> 📸 **For local execution walkthrough with screenshots, see [docs/LocalResults.md](docs/LocalResults.md)**

## What We Built and Why

**The Problem**: Commercial lease abstraction requires 2-4 hours of skilled labor per document. Manual processes lead to errors in CAM recoveries, rent escalations, and critical dates—silent revenue leakage that compounds over entire lease terms.

**The Solution**: An AI-powered extraction pipeline that:
1. **Ingests** lease PDFs and extracts 40-60 critical fields (rent, dates, CAM, options)
2. **Scores** each field with confidence (0-100%) and source citations (page, clause)
3. **Routes** low-confidence fields (<70%) to human reviewers
4. **Stores** versioned, auditable lease data with amendment tracking

**Why It Matters**:
- **Time**: Reduce 2-4 hour manual abstraction to minutes
- **Accuracy**: AI + human review catches errors that cost thousands per lease
- **Compliance**: Full audit trail for every extraction and correction
- **Scale**: Handle acquisition due diligence (100+ leases) without hiring surge staff

## Problem Statement

### The Challenge
Commercial leases are bespoke legal documents spanning **80-150+ pages**. Every downstream system—billing, recoveries, forecasting, critical date management—depends on manual abstraction of **40-60 critical fields** per lease.

### Current Situation (Manual Process)
| Metric | Value | Source |
|--------|-------|--------|
| Time per lease | 2-4 hours | Industry benchmark for skilled lease administrators |
| Loaded labor cost | $60-90/hour | U.S. Bureau of Labor Statistics (Financial Specialists, 2024) |
| Annual abstraction cost | ~$22,500 | Assumes 500 leases, 20% turnover, 3 hrs avg @ $75/hr |
| Recovery leakage | 1-3% of CAM | BOMA International estimates; common in audit findings |
| Revenue impact | $100K-$300K/year | Based on $10M recovery portfolio at 1-3% error rate |

> **Note**: Figures are industry estimates and vary by portfolio size, lease complexity, and regional labor costs. Organizations should validate against their own operational data.

### Why This Matters
- **Acquisition due diligence**: Every deal requires re-abstracting seller's leases under time pressure
- **Amendment compounding**: Each amendment layers on the original, multiplying complexity
- **Silent revenue leakage**: Abstraction errors in CAM caps, escalations, and options go undetected for entire lease terms
- **Audit exposure**: Incorrect recoveries lead to tenant disputes and clawbacks

**Solution**: Automate field extraction using Azure AI Foundry with confidence scoring, source citations, and human review routing for low-confidence items.

## Research Insights

### Why We Chose This Problem

I've watched lease administrators spend entire days buried in 100+ page lease documents, manually keying rent schedules into spreadsheets. The pain is obvious: every acquisition means re-abstracting dozens of leases under deal timelines, every amendment triggers a manual update cascade, and errors in CAM reconciliation don't surface until tenant audits—sometimes years later.

I chose lease abstraction because:
1. **I've seen the workflow firsthand**—it's repetitive, error-prone, and expensive
2. **The data flows downstream everywhere**—billing, recoveries, forecasting, critical dates all depend on accurate abstracts
3. **LLMs are finally good enough**—but only if you architect around their weaknesses (hallucination, no citations, overconfidence)

### What We Learned From Research

- **Domain complexity is real**: Commercial leases aren't standardized. Amendments override originals, options are conditional on tenant performance, and "CAM" means different things in different markets (sometimes including capital, sometimes not)
- **Generic document extraction fails**: Tools like Azure Form Recognizer extract text, but they don't understand that "Base Rent" in Section 3.1 was superseded by Amendment 2, Section 1.4
- **Trust requires transparency**: Lease administrators won't adopt AI extraction unless they can click a field and see exactly which page and clause it came from. Citation-grounded extraction is non-negotiable.

### Why This Is Worth Building

| Factor | Assessment |
|--------|------------|
| **Labor cost** | Abstraction takes 2-4 hours per lease. At $50-75/hour loaded cost (varies by market), that's $100-300 per document. A 500-lease portfolio with 20% annual turnover = 100 abstractions/year = $10K-30K in direct labor. |
| **Error cost** | Industry rule of thumb: 1-2% of CAM billings have abstraction-related errors. On a $10M recovery portfolio, that's $100K-200K at risk annually—but this varies widely by portfolio quality. |
| **Competitive landscape** | Enterprise platforms (Yardi, MRI, Lucernex) charge $50K-200K/year and require long implementations. Generic AI tools don't understand lease semantics. There's a gap for a focused, developer-friendly solution. |

> **Honest caveat**: These numbers are industry estimates, not guarantees. ROI depends on portfolio size, lease complexity, current process maturity, and adoption. We built this to prove the technical approach; actual savings require production validation.

### Competitive Differentiation

The LLM API call is a commodity—anyone can call GPT-4. Sustainable differentiation comes from:

- **Schema design**: Modeling messy lease reality (amendments, conditional options, date hierarchies) correctly
- **Confidence calibration**: Not just "is this right?" but "how sure are we, and should a human review?"
- **Evaluation harness**: Ground-truth test sets to measure extraction accuracy and catch regressions
- **Human review workflow**: The UI and process to efficiently review flagged items, not just dump everything into a queue

This is the engineering around the AI, not the AI itself.

### Key Assumptions & Tradeoffs

| Decision | Assumption | Tradeoff |
|----------|------------|----------|
| **Confidence threshold at 70%** | Fields below 70% need human review | May over-route initially; can tune with production data |
| **Citation-grounded extraction** | Every field must cite page/clause | Slower extraction but prevents hallucination |
| **Single-document focus** | MVP handles one lease at a time | Deferred batch processing and amendment chaining |
| **Azure-native stack** | Target users have Microsoft ecosystem | Limits portability but simplifies auth and deployment |
| **Human-in-the-loop required** | Full automation isn't the goal | Builds trust but requires review queue UI |

## Key Features

### 🤖 AI-Powered Extraction
- Extracts 40-60 predefined lease fields using Azure Foundry Models
- Confidence scores (0-100%) for every extracted field
- Automatic page citations and clause references
- Raw text excerpts for verification

### ✅ Quality Assurance
- Fields <70% confidence automatically routed to review queue
- Human reviewers approve or correct extractions
- Audit trail of all changes and corrections
- Maintains data integrity across amendments

### 📊 Key Extracted Fields
- **Rent**: Base rent schedules, escalations, percentage rent
- **Dates**: Commencement, expiration, renewal/termination options
- **Costs**: CAM expenses, caps, exclusions, security deposits
- **Conditions**: Co-tenancy clauses, notice requirements, guarantees
- **Options**: Renewal, termination, expansion options

### 🏗️ N-Tier Architecture
- **Presentation**: Blazor UI components
- **API**: RESTful controllers with OpenAPI documentation
- **Business Logic**: Service layer with async/await patterns
- **Data Access**: Repository pattern (in-memory MVP, SQL extensible)
- **Domain**: Strongly-typed models and DTOs

### 🔐 Security & Reliability
- **Entra ID + RBAC**: Policy-based authorization (Admin, Reviewer, User)
- **Managed Identity**: Zero secrets in code
- **Key Vault**: Secrets as configuration provider
- **DR/HA**: Cosmos multi-region, Traffic Manager failover, GRS storage

> 📐 **See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed Architecture Pillars (Security, DR, HA, Scalability, Resiliency, Observability)**

## Technology Stack

| Category | Technology |
|----------|------------|
| **Runtime** | .NET 10.0 |
| **Web Framework** | ASP.NET Core 10 with Blazor Server |
| **AI** | Azure AI Foundry / OpenAI |
| **Authentication** | Microsoft Entra ID (OAuth 2.0 / OIDC) |
| **Authorization** | Role-Based Access Control (RBAC) |
| **Service Auth** | Managed Identity + DefaultAzureCredential |
| **Database** | Azure Cosmos DB (document metadata) |
| **Storage** | Azure Blob Storage (lease PDFs) |
| **Secrets** | Azure Key Vault |
| **Logging** | Application Insights + ILogger |
| **PDF Processing** | PdfPig (UglyToad.PdfPig) |
| **Testing** | xUnit + Moq + FluentAssertions |
| **API Docs** | Scalar (OpenAPI) |
| **Async Processing** | Azure Service Bus + Function Apps *(planned)* |



## Architecture Overview

> 📐 **See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed N-Tier layers, Mermaid diagrams, and component documentation.**

## Configuration & Deployment

> 📋 **See [docs/SETUP_GUIDE.md](docs/SETUP_GUIDE.md) for:**
> - `appsettings.json` configuration reference
> - Environment variables
> - Azure CLI scripts (App Service, Storage, Key Vault, Cosmos DB, etc.)
> - Managed Identity role assignments
> - Troubleshooting guide

> 📋 **See [docs/SETUP_GUIDE.md](docs/SETUP_GUIDE.md) for complete Azure CLI scripts:**
> - App Service, Storage Account, Key Vault, Cosmos DB
> - Service Bus, Function App, Application Insights
> - Managed Identity role assignments

## Code Style

Follows [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):
- PascalCase for public members
- camelCase for private fields
- Async methods end with `Async`
- Use `var` for obvious types, explicit for clarity
- Modern C# features (records, nullable checks, etc.)

## Roadmap

### Implemented (MVP)
- Azure Web App (Blazor Server + Entra ID)
- Azure Cosmos DB, Blob Storage, Key Vault
- Azure AI Foundry, Application Insights

### Future Work
| Phase | Features |
|-------|----------|
| **Phase 2** | Service Bus async processing, Function App triggers, APIM AI Gateway, batch uploads |
| **Phase 3** | Multi-region, Azure AI Search, SignalR notifications |
| **Phase 3+** | Reduce human review via Prompt Flow evals, confidence calibration, fine-tuning |

> 📐 **See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for Architecture Pillars (Security, DR, HA, Scalability, Resiliency) and detailed roadmap.**
