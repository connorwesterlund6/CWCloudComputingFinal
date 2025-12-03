using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// ### BEGIN CHANGES ###

// Get the Key Vault URI from environment configuration
var keyVaultUri = new Uri(Environment.GetEnvironmentVariable("KEY_VAULT_URI") 
    ?? throw new InvalidOperationException("KEY_VAULT_URI is not set."));

// Configure the credential to ONLY use Managed Identity (Production) or Azure CLI (Local)
var credentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeEnvironmentCredential = true,
    ExcludeWorkloadIdentityCredential = true,
    ExcludeVisualStudioCredential = true,
    ExcludeVisualStudioCodeCredential = true, // This stops the WAM error
    ExcludeInteractiveBrowserCredential = true, // This stops the popup
    ExcludeAzurePowerShellCredential = true

    
    // AzureCliCredential and ManagedIdentityCredential are TRUE by default.
};

// Register the SecretClient with these specific options
builder.Services.AddSingleton(new SecretClient(keyVaultUri, new DefaultAzureCredential(credentialOptions)));

// ### END CHANGES ###

builder.Build().Run();