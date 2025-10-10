using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MovieAPI;

// Represents a movie entity.
public class Movie
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public int Year { get; set; }
}

/// <summary>
/// Contains all the HTTP-triggered functions and the in-memory data for the Movie API.
/// </summary>
public class MovieFunctions
{
    // A static list to act as a simple in-memory database.
    private static readonly List<Movie> MovieAPI = new List<Movie>
    {
        new Movie { Id = 1, Title = "The Shawshank Redemption", Genre = "Drama", Year = 1994 },
        new Movie { Id = 2, Title = "The Godfather", Genre = "Crime", Year = 1972 },
        new Movie { Id = 3, Title = "The Dark Knight", Genre = "Action", Year = 2008 },
        new Movie { Id = 4, Title = "Pulp Fiction", Genre = "Crime", Year = 1994 }
    };


    private static int GetNextId() => (MovieAPI.Any()) ? MovieAPI.Max(m => m.Id) + 1 : 1;

    private bool IsApiKeyValid(HttpRequestData req)
    {
      
        const string requiredApiKey = "123456";
        
        
        if (req.Headers.TryGetValues("X-Api-Key", out var values))
        {
            var extractedApiKey = values.FirstOrDefault();
            return requiredApiKey.Equals(extractedApiKey);
        }
        
        return false;
    }

    // GET /movies
    [Function("GetMovieAPI")]
    public async Task<HttpResponseData> GetMovieAPI(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "MovieAPI")] HttpRequestData req)
    {
        if (!IsApiKeyValid(req))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(MovieAPI);
        return response;
    }

    // GET /movies/{id}
    [Function("GetMovieById")]
    public async Task<HttpResponseData> GetMovieById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "movieAPI/{id}")] HttpRequestData req, int id)
    {
        if (!IsApiKeyValid(req))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }
        
        var movie = MovieAPI.FirstOrDefault(m => m.Id == id);
        if (movie == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(movie);
        return response;
    }

    // POST /movies
    [Function("CreateMovieAPI")]
    public async Task<HttpResponseData> CreateMovie(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "MovieAPI")] HttpRequestData req)
    {
        if (!IsApiKeyValid(req))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var newMovie = await JsonSerializer.DeserializeAsync<Movie>(req.Body);
        if (newMovie == null || string.IsNullOrWhiteSpace(newMovie.Title))
        {
             var badReqResponse = req.CreateResponse(HttpStatusCode.BadRequest);
             await badReqResponse.WriteStringAsync("Movie title is required.");
             return badReqResponse;
        }

        newMovie.Id = GetNextId();
        MovieAPI.Add(newMovie);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(newMovie);
        return response;
    }

    // PUT /movies/{id}
    [Function("UpdateMovieAPI")]
    public async Task<HttpResponseData> UpdateMovie(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "MovieAPI/{id}")] HttpRequestData req, int id)
    {
        if (!IsApiKeyValid(req))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var movie = MovieAPI.FirstOrDefault(m => m.Id == id);
        if (movie == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var updatedMovie = await JsonSerializer.DeserializeAsync<Movie>(req.Body);
        if (updatedMovie == null || string.IsNullOrWhiteSpace(updatedMovie.Title))
        {
            var badReqResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badReqResponse.WriteStringAsync("Movie title is required.");
            return badReqResponse;
        }

        movie.Title = updatedMovie.Title;
        movie.Genre = updatedMovie.Genre;
        movie.Year = updatedMovie.Year;

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(movie);
        return response;
    }
    
    // DELETE /movies/{id}
    [Function("DeleteMovieAPI")]
    public HttpResponseData DeleteMovie(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "MovieAPI/{id}")] HttpRequestData req, int id)
    {
        if (!IsApiKeyValid(req))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var movie = MovieAPI.FirstOrDefault(m => m.Id == id);
        if (movie == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        
        MovieAPI.Remove(movie);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}