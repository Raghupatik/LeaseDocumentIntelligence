# Lease Document Intelligence - Setup Guide

## Prerequisites

- **.NET 10.0 SDK** or later
- **Visual Studio 2026** or Visual Studio Code
- **Azure Account** with Foundry/OpenAI resource
- **Git** for version control

## Local Development Setup

### 1. Clone Repository
```bash
git clone https://github.com/your-org/LeaseDocumentIntelligence.git
cd LeaseDocumentIntelligence
```

### 2. Configure Foundry Model Endpoint

Create `appsettings.Development.json`:
```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Debug",
	  "Microsoft.AspNetCore": "Warning"
	}
  },
  "Foundry": {
	"Endpoint": "https://your-foundry-resource.openai.azure.com/",
	"ModelName": "gpt-4",
	"UseLocalTesting": true
  }
}
```

Replace:
- `your-foundry-resource` - Your Azure Foundry resource name
- `gpt-4` - Your deployed model name

### 3. Authenticate with Azure CLI

For local development, authenticate using:
```bash
az login
```

The `DefaultAzureCredential` will automatically use your CLI context.

### 4. Restore Dependencies

```bash
dotnet restore
```

### 5. Run the Application

```bash
cd LeaseDocumentIntelligence
dotnet run
```

Application will start at `https://localhost:5001` (or next available port)

**API Documentation**: Access interactive API docs at `/scalar/v1`

### 6. Run Unit Tests

```bash
dotnet test LeaseDocumentIntelligence.Tests
```

## Azure Setup

### 1. Create Foundry/OpenAI Resource

```bash
az cognitiveservices account create \
  --name lease-ai-resource \
  --resource-group your-resource-group \
  --kind OpenAI \
  --sku S0 \
  --location eastus \
  --assign-identity
```

### 2. Deploy a Model

Deploy `gpt-4` or `gpt-35-turbo` model:
```bash
az cognitiveservices account deployment create \
  --name lease-ai-resource \
  --resource-group your-resource-group \
  --deployment-name gpt-4 \
  --model-name gpt-4 \
  --model-version "0613"
```

### 3. Get Resource Details

```bash
# Get endpoint
az cognitiveservices account show \
  --name lease-ai-resource \
  --resource-group your-resource-group \
  --query properties.endpoint
```

### 4. Set Managed Identity Permissions

Grant your app's managed identity the `Cognitive Services OpenAI User` role:

```bash
# Get app identity object ID
APP_OBJECT_ID=$(az identity show \
  --name <app-name> \
  --resource-group your-resource-group \
  --query principalId -o tsv)

# Grant role
az role assignment create \
  --assignee-object-id $APP_OBJECT_ID \
  --role "Cognitive Services OpenAI User" \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account-name>
```

## Deployment to Azure App Service

### 1. Create App Service

```bash
az appservice plan create \
  --name lease-plan \
  --resource-group your-resource-group \
  --sku B2 --is-linux

az webapp create \
  --resource-group your-resource-group \
  --plan lease-plan \
  --name lease-app \
  --runtime "DOTNET|8.0"
```

### 2. Enable Managed Identity

```bash
az webapp identity assign \
  --resource-group your-resource-group \
  --name lease-app
```

### 3. Configure Application Settings

```bash
az webapp config appsettings set \
  --resource-group your-resource-group \
  --name lease-app \
  --settings \
	"Foundry__Endpoint=https://your-foundry-resource.openai.azure.com/" \
	"Foundry__ModelName=gpt-4" \
	"ASPNETCORE_ENVIRONMENT=Production"
```

### 4. Deploy from GitHub

Option A: Using GitHub Actions (recommended)
1. Create `.github/workflows/deploy.yml`
2. Configure Azure login action
3. Push code to deploy

Option B: Using Azure CLI
```bash
az webapp up \
  --resource-group your-resource-group \
  --name lease-app \
  --runtime "DOTNET|10.0" \
  --os-type Windows
```

## Azure Infrastructure Setup

### Azure Storage Account
```bash
# Create Storage Account
az storage account create \
  --resource-group my-rg \
  --name myleasepdfstorage \
  --sku Standard_GRS \
  --kind StorageV2 \
  --access-tier Hot

# Create blob container for lease PDFs
az storage container create \
  --account-name myleasepdfstorage \
  --name lease-documents \
  --auth-mode login

# Grant Web App Managed Identity access
az role assignment create \
  --assignee-object-id $WEBAPP_IDENTITY_ID \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/.../resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/myleasepdfstorage
```

### Azure Key Vault
```bash
# Create Key Vault
az keyvault create \
  --resource-group my-rg \
  --name my-lease-kv \
  --enable-rbac-authorization true

# Grant Web App Managed Identity access
az role assignment create \
  --assignee-object-id $WEBAPP_IDENTITY_ID \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/.../resourceGroups/my-rg/providers/Microsoft.KeyVault/vaults/my-lease-kv

# Add secrets (use -- for hierarchy)
az keyvault secret set --vault-name my-lease-kv --name "AzureAd--ClientSecret" --value "<secret>"
az keyvault secret set --vault-name my-lease-kv --name "CosmosDb--ConnectionString" --value "<conn-string>"
```

### Azure Cosmos DB
```bash
# Create Cosmos DB account
az cosmosdb create \
  --resource-group my-rg \
  --name my-lease-cosmos \
  --kind GlobalDocumentDB \
  --default-consistency-level Session

# Create database and container
az cosmosdb sql database create \
  --resource-group my-rg \
  --account-name my-lease-cosmos \
  --name LeaseDB

az cosmosdb sql container create \
  --resource-group my-rg \
  --account-name my-lease-cosmos \
  --database-name LeaseDB \
  --name Documents \
  --partition-key-path "/tenantId"

# Grant Managed Identity access
az cosmosdb sql role assignment create \
  --resource-group my-rg \
  --account-name my-lease-cosmos \
  --role-definition-name "Cosmos DB Built-in Data Contributor" \
  --principal-id $WEBAPP_IDENTITY_ID \
  --scope /subscriptions/.../resourceGroups/my-rg/providers/Microsoft.DocumentDB/databaseAccounts/my-lease-cosmos
```

### Azure Service Bus (Phase 2)
```bash
# Create Service Bus namespace
az servicebus namespace create \
  --resource-group my-rg \
  --name my-lease-servicebus \
  --sku Standard \
  --location eastus

# Create queue for document processing
az servicebus queue create \
  --resource-group my-rg \
  --namespace-name my-lease-servicebus \
  --name document-processing \
  --max-size 1024 \
  --default-message-time-to-live P14D

# Grant Web App permission to send messages
az role assignment create \
  --assignee-object-id $WEBAPP_IDENTITY_ID \
  --role "Azure Service Bus Data Sender" \
  --scope /subscriptions/.../resourceGroups/my-rg/providers/Microsoft.ServiceBus/namespaces/my-lease-servicebus

# Grant Function App permission to receive messages
az role assignment create \
  --assignee-object-id $FUNC_IDENTITY_ID \
  --role "Azure Service Bus Data Receiver" \
  --scope /subscriptions/.../resourceGroups/my-rg/providers/Microsoft.ServiceBus/namespaces/my-lease-servicebus
```

### Azure Function App (Phase 2)
```bash
# Create Function App
az functionapp create \
  --resource-group my-rg \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 10.0 \
  --functions-version 4 \
  --name my-lease-functions \
  --storage-account myleasepdfstorage

# Enable Managed Identity
az functionapp identity assign --resource-group my-rg --name my-lease-functions

# Configure Service Bus connection
az functionapp config appsettings set \
  --resource-group my-rg \
  --name my-lease-functions \
  --settings "ServiceBusConnection__fullyQualifiedNamespace=my-lease-servicebus.servicebus.windows.net"

# Grant Function App access to AI Foundry
az role assignment create \
  --assignee-object-id $FUNC_IDENTITY_ID \
  --role "Cognitive Services OpenAI User" \
  --scope /subscriptions/.../resourceGroups/.../providers/Microsoft.CognitiveServices/accounts/...

# Grant Function App access to Cosmos DB
az cosmosdb sql role assignment create \
  --resource-group my-rg \
  --account-name my-lease-cosmos \
  --role-definition-name "Cosmos DB Built-in Data Contributor" \
  --principal-id $FUNC_IDENTITY_ID \
  --scope /subscriptions/.../resourceGroups/my-rg/providers/Microsoft.DocumentDB/databaseAccounts/my-lease-cosmos
```

### Azure Application Insights
```bash
# Create Application Insights
az monitor app-insights component create \
  --resource-group my-rg \
  --app my-lease-appinsights \
  --location eastus \
  --kind web

# Get connection string and add to Key Vault
CONNECTION_STRING=$(az monitor app-insights component show \
  --resource-group my-rg \
  --app my-lease-appinsights \
  --query connectionString -o tsv)

az keyvault secret set \
  --vault-name my-lease-kv \
  --name "ApplicationInsights--ConnectionString" \
  --value "$CONNECTION_STRING"
```

## Configuration Reference

### appsettings.json (Production)
```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Microsoft.AspNetCore": "Warning"
	}
  },
  "Foundry": {
	"Endpoint": "https://{resource-name}.openai.azure.com/",
	"ModelName": "gpt-4",
	"ApiVersion": "2024-02-15-preview"
  },
  "FileUpload": {
	"MaxFileSizeBytes": 52428800,
	"AllowedContentTypes": ["application/pdf"]
  }
}
```

### Environment Variables (Override appsettings)
- `Foundry__Endpoint` - Foundry endpoint URL
- `Foundry__ModelName` - Deployed model name
- `Foundry__ApiVersion` - API version (optional)
- `ASPNETCORE_ENVIRONMENT` - Environment (Development/Production)
- `ASPNETCORE_URLS` - Binding URLs (if needed)

## Troubleshooting

### Issue: 401 Unauthorized from Foundry API

**Solution**: Verify Managed Identity has correct role assignment
```bash
az role assignment list --assignee <identity-id>
```

### Issue: PDF extraction fails

**Solution**: Ensure PdfPig package is properly installed
```bash
dotnet add package UglyToad.PdfPig
```

### Issue: Cannot connect to local Foundry

**Solution**: Check endpoint URL format - should be `https://{resource}.openai.azure.com/`

### Issue: Blazor components not rendering

**Solution**: 
1. Clear browser cache (Ctrl+Shift+Del)
2. Rebuild solution: `dotnet build`
3. Restart development server

## Database Migration (Future)

When migrating from in-memory to SQL Server:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Security Best Practices

### Development
- Use `appsettings.Development.json` with sensitive values
- Add to `.gitignore` - never commit secrets
- Use Azure CLI for authentication

### Production
- Use Managed Identity only (no connection strings)
- Store non-secret config in Application Settings
- Use Azure Key Vault for sensitive values
- Enable HTTPS only
- Configure CORS for specific origins
- Set Content Security Policy headers

## Next Steps

1. **Run unit tests** to verify setup:
   ```bash
   dotnet test
   ```

2. **Upload test lease** via UI at `/upload`

3. **Review extracted fields** with confidence scores

4. **Monitor logs** in `logs/` directory

5. **Check API documentation** at Swagger UI (development only)

## Support

For issues or questions:
1. Check logs in `logs/` directory
2. Review Azure Monitor (Application Insights)
3. Check GitHub Issues
4. Contact engineering team

## Code Style & Conventions

This project follows Microsoft's C# Coding Conventions:
- https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions

**Key Guidelines**:
- Use PascalCase for public members
- Use camelCase for private fields
- Async methods end with `Async`
- Use `var` for obvious types, specify for clarity
- Use modern C# features (records, nullability checks, etc.)
- XML comments for public APIs

## License

MIT License - See LICENSE file for details
