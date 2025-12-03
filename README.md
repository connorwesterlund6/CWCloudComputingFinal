# 🎬 Movie API (Azure Functions Midterm)

A Serverless REST API built with **.NET 9** and **Azure Functions** to manage a movie database. This project demonstrates secure cloud architecture, automated maintenance tasks, and real-time telemetry.

## 🚀 Features

* **Full CRUD Functionality:** Create, Read, Update, and Delete movies from an Azure SQL Database.
* **Security:** API Key authentication enforced on all endpoints, with keys securely stored in **Azure Key Vault**.
* **Automated Maintenance:** A **Logic App** runs on a schedule to trigger validation logic (e.g., auto-tagging old movies as "Classic").
* **Telemetry:** Integrated **Application Insights** to track custom events, user actions, and exceptions.
* **Serverless Architecture:** Built on Azure Functions v4 (Isolated Worker Model).

---

## 🛠️ Tech Stack

* **Framework:** .NET 9.0 (Isolated Worker)
* **Cloud Platform:** Microsoft Azure
* **Compute:** Azure Functions
* **Database:** Azure SQL Database
* **Security:** Azure Key Vault (Managed Identity)
* **Automation:** Azure Logic Apps (Consumption Plan)
* **Monitoring:** Azure Application Insights

---

## 🔗 API Endpoints

All endpoints require the `X-Api-Key` header for authentication.

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **GET** | `/api/MovieAPI` | Retrieve a list of all movies. |
| **GET** | `/api/MovieAPI/{id}` | Retrieve a specific movie by ID. |
| **POST** | `/api/MovieAPI` | Create a new movie. |
| **PUT** | `/api/MovieAPI/{id}` | Update an existing movie. |
| **DELETE**| `/api/MovieAPI/{id}` | Delete a movie. |
| **PATCH** | `/api/movies/validate` | **Admin:** Checks for movies released before 1990 and updates their genre. |

### 📝 Request Examples

**1. Create a Movie (POST)**
```json
{
  "Title": "Fight Club",
  "Genre": "Action",
  "Year": 1999
}
