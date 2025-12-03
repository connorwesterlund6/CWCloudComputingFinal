using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq; // Added for Linq
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient; 
using System.Data; 
using Microsoft.ApplicationInsights; // Added for Telemetry
using Microsoft.ApplicationInsights.DataContracts; // Added for Telemetry

namespace MovieAPI;

public class Movie
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public int Year { get; set; }
}

public class ValidationResponse
{
    public int UpdatedCount { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class MovieFunctions
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<MovieFunctions> _logger;
    private readonly TelemetryClient _telemetryClient; // REQUIRED FOR DELIVERABLE 5
    private static string? _cachedApiKey;
    private readonly string _connectionString;

    // Inject TelemetryClient here
    public MovieFunctions(SecretClient secretClient, ILogger<MovieFunctions> logger, TelemetryClient telemetryClient)
    {
        _secretClient = secretClient;
        _logger = logger;
        _telemetryClient = telemetryClient;
        
        _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString") 
                            ?? throw new InvalidOperationException("Connection string 'SqlConnectionString' not found.");
    }

    // --- SECURITY CHECK ---
    private async Task<bool> IsApiKeyValid(HttpRequestData req)
    {
        if (string.IsNullOrEmpty(_cachedApiKey))
        {
            try
            {
                KeyVaultSecret secret = await _secretClient.GetSecretAsync("ApiKey");
                _cachedApiKey = secret.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching 'ApiKey' secret from Key Vault.");
                // Track exception in App Insights
                _telemetryClient.TrackException(ex);
                return false;
            }
        }

        if (req.Headers.TryGetValues("X-Api-Key", out var values))
        {
            bool isValid = _cachedApiKey != null && _cachedApiKey.Equals(values.FirstOrDefault());
            if (!isValid) 
            {
                _logger.LogWarning("Invalid API Key attempt.");
                _telemetryClient.TrackEvent("SecurityAlert-InvalidKeyAttempt");
            }
            return isValid;
        }
        return false;
    }

    private Movie MapToMovie(SqlDataReader reader)
    {
        return new Movie
        {
            Id = (int)reader["Id"],
            Title = reader["Title"].ToString(),
            Genre = reader["Genre"] is DBNull ? null : reader["Genre"].ToString(),
            Year = reader["Year"] is DBNull ? 0 : (int)reader["Year"]
        };
    }

    // Logic: If Year < 1990 and Genre doesn't say "Classic", add "Classic" to the genre.
    [Function("ValidateMovies")]
    public async Task<HttpResponseData> ValidateMovies(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "movies/validate")] HttpRequestData req)
    {
        // 1. Security Check
        if (!await IsApiKeyValid(req)) return req.CreateResponse(HttpStatusCode.Unauthorized);

        _logger.LogInformation("Validation logic triggered.");
        
        // 2. Track Custom Event (Deliverable 5)
        _telemetryClient.TrackEvent("ValidationTriggered", new Dictionary<string, string> { { "TriggeredBy", "LogicAppOrUser" } });

        int updatedCount = 0;

        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            
            // This SQL updates old movies to be "Classic" if they aren't already marked as such
            var query = @"
                UPDATE Movies 
                SET Genre = 'Classic ' + ISNULL(Genre, '')
                WHERE Year < 1990 
                AND (Genre NOT LIKE '%Classic%' OR Genre IS NULL)";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                updatedCount = await cmd.ExecuteNonQueryAsync();
            }
        }

        _telemetryClient.TrackMetric("MoviesAutoArchivedCount", updatedCount);

        var responseObj = new ValidationResponse
        {
            UpdatedCount = updatedCount,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Message = updatedCount > 0 ? "Old movies tagged as Classic." : "No updates needed."
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(responseObj);
        return response;
    }

    // --- EXISTING CRUD ---

    [Function("GetMovieAPI")]
    public async Task<HttpResponseData> GetMovieAPI(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "MovieAPI")] HttpRequestData req)
    {
        if (!await IsApiKeyValid(req)) return req.CreateResponse(HttpStatusCode.Unauthorized);

        var movieList = new List<Movie>();
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var query = "SELECT Id, Title, Genre, Year FROM Movies";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) movieList.Add(MapToMovie(reader));
            }
        }
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(movieList);
        return response;
    }

    [Function("GetMovieById")]
    public async Task<HttpResponseData> GetMovieById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "movieAPI/{id}")] HttpRequestData req, int id)
    {
        if (!await IsApiKeyValid(req)) return req.CreateResponse(HttpStatusCode.Unauthorized);

        Movie? movie = null;
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var query = "SELECT Id, Title, Genre, Year FROM Movies WHERE Id = @Id";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync()) movie = MapToMovie(reader);
                }
            }
        }
        if (movie == null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(movie);
        return response;
    }

    [Function("CreateMovieAPI")]
    public async Task<HttpResponseData> CreateMovie(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "MovieAPI")] HttpRequestData req)
    {
        if (!await IsApiKeyValid(req)) return req.CreateResponse(HttpStatusCode.Unauthorized);

        var newMovie = await JsonSerializer.DeserializeAsync<Movie>(req.Body);
        if (newMovie == null || string.IsNullOrWhiteSpace(newMovie.Title))
        {
             var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
             await badReq.WriteStringAsync("Movie title is required.");
             return badReq;
        }

        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var query = "INSERT INTO Movies (Title, Genre, Year) OUTPUT INSERTED.Id VALUES (@Title, @Genre, @Year)";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Title", newMovie.Title);
                cmd.Parameters.AddWithValue("@Genre", (object?)newMovie.Genre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Year", newMovie.Year);
                newMovie.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync()); 
            }
        }

        _telemetryClient.TrackEvent("MovieCreated", new Dictionary<string, string> { { "MovieTitle", newMovie.Title } });

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(newMovie);
        return response;
    }

    [Function("UpdateMovieAPI")]
    public async Task<HttpResponseData> UpdateMovie(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "MovieAPI/{id}")] HttpRequestData req, int id)
    {
        if (!await IsApiKeyValid(req)) return req.CreateResponse(HttpStatusCode.Unauthorized);

        var updatedMovie = await JsonSerializer.DeserializeAsync<Movie>(req.Body);
        if (updatedMovie == null || string.IsNullOrWhiteSpace(updatedMovie.Title))
        {
            var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
            await badReq.WriteStringAsync("Movie title is required.");
            return badReq;
        }

        int rowsAffected;
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var query = "UPDATE Movies SET Title = @Title, Genre = @Genre, Year = @Year WHERE Id = @Id";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Title", updatedMovie.Title);
                cmd.Parameters.AddWithValue("@Genre", (object?)updatedMovie.Genre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Year", updatedMovie.Year);
                rowsAffected = await cmd.ExecuteNonQueryAsync();
            }
        }

        if (rowsAffected == 0) return req.CreateResponse(HttpStatusCode.NotFound);
        updatedMovie.Id = id;
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(updatedMovie);
        return response;
    }
    
    [Function("DeleteMovieAPI")]
    public async Task<HttpResponseData> DeleteMovie(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "MovieAPI/{id}")] HttpRequestData req, int id)
    {
        if (!await IsApiKeyValid(req)) return req.CreateResponse(HttpStatusCode.Unauthorized);

        int rowsAffected;
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var query = "DELETE FROM Movies WHERE Id = @Id";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                rowsAffected = await cmd.ExecuteNonQueryAsync();
            }
        }

        if (rowsAffected == 0) return req.CreateResponse(HttpStatusCode.NotFound);
        
        _telemetryClient.TrackEvent("MovieDeleted", new Dictionary<string, string> { { "MovieId", id.ToString() } });

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}