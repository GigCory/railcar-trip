using System.Net.Http.Json;
using RailcarTrips.Shared.DTOs;

namespace RailcarTrips.Client.Services;

public class TripService : ITripService
{
    private readonly HttpClient _httpClient;

    public TripService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UploadResultDto> UploadEventsAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync("api/trips/upload", content);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UploadResultDto>()
                ?? new UploadResultDto { Errors = new List<string> { "Failed to parse response" } };
        }

        var error = await response.Content.ReadFromJsonAsync<UploadResultDto>();
        return error ?? new UploadResultDto { Errors = new List<string> { $"HTTP Error: {response.StatusCode}" } };
    }

    public async Task<List<TripDto>> GetTripsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TripDto>>("api/trips")
                ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching trips: {ex.Message}");
            return new List<TripDto>();
        }
    }

    public async Task<TripWithEventsDto?> GetTripDetailsAsync(int tripId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TripWithEventsDto>($"api/trips/{tripId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching trip details: {ex.Message}");
            return null;
        }
    }
}
