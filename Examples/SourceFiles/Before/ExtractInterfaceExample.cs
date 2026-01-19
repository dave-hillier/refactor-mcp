using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Examples.ExtractInterface;

/// <summary>
/// Example: WeatherService that should have an interface extracted for testability.
/// Refactoring: extract-interface to create IWeatherService with GetCurrentWeatherAsync, GetForecastAsync
/// </summary>
public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public WeatherService(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    // These public methods should be in the extracted interface
    public async Task<WeatherForecast> GetCurrentWeatherAsync(string city)
    {
        var url = $"https://api.weather.com/v1/current?city={city}&key={_apiKey}";
        // Simulated implementation
        await Task.Delay(10);
        return new WeatherForecast
        {
            City = city,
            Temperature = 72,
            Conditions = "Sunny"
        };
    }

    public async Task<WeatherForecast[]> GetForecastAsync(string city, int days)
    {
        var url = $"https://api.weather.com/v1/forecast?city={city}&days={days}&key={_apiKey}";
        // Simulated implementation
        await Task.Delay(10);
        return new[]
        {
            new WeatherForecast { City = city, Temperature = 70, Conditions = "Cloudy" },
            new WeatherForecast { City = city, Temperature = 75, Conditions = "Sunny" }
        };
    }

    public async Task<WeatherAlert[]> GetAlertsAsync(string region)
    {
        var url = $"https://api.weather.com/v1/alerts?region={region}&key={_apiKey}";
        // Simulated implementation
        await Task.Delay(10);
        return Array.Empty<WeatherAlert>();
    }
}

// Supporting types
public class HttpClient
{
    public Task<string> GetStringAsync(string url) => Task.FromResult("{}");
}

public class WeatherForecast
{
    public string City { get; set; } = "";
    public double Temperature { get; set; }
    public string Conditions { get; set; } = "";
}

public class WeatherAlert
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
