#  Movie API (Azure Functions Midterm)

## Project Overview

This project is a Serverless REST API built using **.NET 9** and **Azure Functions (Isolated Worker Model)**. It provides a robust backend for managing a movie database with comprehensive CRUD (Create, Read, Update, Delete) capabilities.

Key architectural features include:

* **Persistent Storage:** Data is stored in **Azure SQL Database** using raw SQL interactions (`Microsoft.Data.SqlClient`) for performance.
* **Security:** All endpoints are protected via API Key authentication, with secrets securely managed in **Azure Key Vault** and accessed via Managed Identity.
* **Automation:** An Azure Logic App triggers a scheduled maintenance task to validate and update movie records automatically.
* **Observability:** Integrated **Application Insights** provides real-time telemetry, custom event tracking, and failure diagnostics.

## Setup Instructions

### Prerequisites

* .NET 9.0 SDK
* Visual Studio Code or Visual Studio 2022
* Azure Functions Core Tools v4
* An Azure Subscription

### 1. Installation

Clone the repository and restore dependencies:

```bash
git clone [https://github.com/YOUR-USERNAME/YOUR-REPO-NAME.git](https://github.com/YOUR-USERNAME/YOUR-REPO-NAME.git)
cd CWMovieApiMidterm
dotnet restore
````

### 2\. Local Configuration

Create a file named `local.settings.json` in the root directory. **Do not commit this file.** Add your connection strings and Key Vault URI:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "KEY_VAULT_URI": "https://<your-keyvault-name>.vault.azure.net/",
    "SqlConnectionString": "Server=tcp:<your-server>.database.windows.net...;Authentication=Active Directory Default;"
  }
}
```

### 3\. Azure Configuration

When deploying to Azure, ensure the following **Environment Variables** are set in the Function App settings:

  * `KEY_VAULT_URI`
  * `SqlConnectionString`

Ensure your Function App's **Managed Identity** is enabled and has the **Key Vault Secrets User** role assigned in your Key Vault.

### 4\. Running the Project

Start the local function host:

```bash
func start
```

## API Reference

**Authentication:** All requests must include the following HTTP header:

  * **Key:** `X-Api-Key`
  * **Value:** *\<Your-Secret-From-KeyVault\>*

###  Movies

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/MovieAPI` | Retrieves a list of all movies in the database. |
| `GET` | `/api/MovieAPI/{id}` | Retrieves a single movie by its numeric ID. |
| `POST` | `/api/MovieAPI` | Creates a new movie. Requires a JSON body. |
| `PUT` | `/api/MovieAPI/{id}` | Updates an existing movie. Requires a JSON body. |
| `DELETE` | `/api/MovieAPI/{id}` | Deletes a movie by ID. |

###  Maintenance

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `PATCH` | `/api/movies/validate` | **Validation Task:** Checks for movies released before 1990 and appends "(Classic)" to their genre. Logs a `ValidationTriggered` custom event to App Insights. |

### Sample Request Body (POST/PUT)

```json
{
  "Title": "Inception",
  "Genre": "Sci-Fi",
  "Year": 2010
}
```

## Telemetry & Monitoring

The API logs specific custom events to Azure Application Insights:

  * `MovieCreated`: Triggered on successful POST requests.
  * `MovieDeleted`: Triggered on successful DELETE requests.
  * `ValidationTriggered`: Triggered by the automated Logic App task.
  * `SecurityAlert-InvalidKeyAttempt`: Triggered when an incorrect API Key is used.

<!-- end list -->
