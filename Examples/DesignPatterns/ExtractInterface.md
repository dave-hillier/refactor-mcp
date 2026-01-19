# Extract Interface Refactoring

## Overview
The `extract-interface` refactoring creates a new interface based on selected members of a class. The class is then modified to implement the new interface.

## When to Use
- When you need to mock a class for testing
- When multiple classes should share a common contract
- When decoupling consumers from specific implementations
- When preparing for dependency injection

---

## Example 1: Extract Interface for Testability

### Before
```csharp
public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, string apiKey, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<WeatherForecast> GetCurrentWeatherAsync(string city)
    {
        var url = $"https://api.weather.com/v1/current?city={city}&key={_apiKey}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WeatherForecast>(json);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch weather for {City}", city);
            throw new WeatherServiceException($"Could not fetch weather for {city}", ex);
        }
    }

    public async Task<WeatherForecast[]> GetForecastAsync(string city, int days)
    {
        var url = $"https://api.weather.com/v1/forecast?city={city}&days={days}&key={_apiKey}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WeatherForecast[]>(json);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch forecast for {City}", city);
            throw new WeatherServiceException($"Could not fetch forecast for {city}", ex);
        }
    }

    public async Task<WeatherAlert[]> GetAlertsAsync(string region)
    {
        var url = $"https://api.weather.com/v1/alerts?region={region}&key={_apiKey}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<WeatherAlert[]>(json);
    }
}

// Testing is difficult - needs actual HTTP calls
[Fact]
public async Task GetCurrentWeather_ReturnsWeather()
{
    var service = new WeatherService(new HttpClient(), "real-key", logger);
    var weather = await service.GetCurrentWeatherAsync("London"); // Hits real API!
    Assert.NotNull(weather);
}
```

### After
```csharp
// IWeatherService.cs - New interface
public interface IWeatherService
{
    Task<WeatherForecast> GetCurrentWeatherAsync(string city);
    Task<WeatherForecast[]> GetForecastAsync(string city, int days);
    Task<WeatherAlert[]> GetAlertsAsync(string region);
}

// WeatherService.cs - Now implements interface
public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, string apiKey, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<WeatherForecast> GetCurrentWeatherAsync(string city)
    {
        // Implementation unchanged
    }

    public async Task<WeatherForecast[]> GetForecastAsync(string city, int days)
    {
        // Implementation unchanged
    }

    public async Task<WeatherAlert[]> GetAlertsAsync(string region)
    {
        // Implementation unchanged
    }
}

// Testing is now easy with mocks
[Fact]
public async Task ProcessWeatherData_FormatsCorrectly()
{
    var mockWeatherService = new Mock<IWeatherService>();
    mockWeatherService
        .Setup(w => w.GetCurrentWeatherAsync("London"))
        .ReturnsAsync(new WeatherForecast
        {
            Temperature = 18,
            Conditions = "Cloudy"
        });

    var processor = new WeatherDataProcessor(mockWeatherService.Object);
    var result = await processor.GetFormattedWeatherAsync("London");

    Assert.Equal("London: 18°C, Cloudy", result);
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-interface '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/WeatherService.cs",
    "interfaceName": "IWeatherService",
    "memberNames": ["GetCurrentWeatherAsync", "GetForecastAsync", "GetAlertsAsync"]
}'
```

---

## Example 2: Extract Interface for Multiple Implementations

### Before
```csharp
public class SqlNotificationRepository
{
    private readonly string _connectionString;

    public SqlNotificationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Notification> GetByIdAsync(Guid id)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<Notification>(
            "SELECT * FROM Notifications WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<Notification>> GetUnreadAsync(Guid userId)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Notification>(
            "SELECT * FROM Notifications WHERE UserId = @UserId AND IsRead = 0",
            new { UserId = userId });
    }

    public async Task CreateAsync(Notification notification)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            @"INSERT INTO Notifications (Id, UserId, Title, Body, IsRead, CreatedAt)
              VALUES (@Id, @UserId, @Title, @Body, @IsRead, @CreatedAt)",
            notification);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE Notifications SET IsRead = 1 WHERE Id = @Id",
            new { Id = id });
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM Notifications WHERE Id = @Id",
            new { Id = id });
    }
}
```

### After
```csharp
// INotificationRepository.cs
public interface INotificationRepository
{
    Task<Notification> GetByIdAsync(Guid id);
    Task<IEnumerable<Notification>> GetUnreadAsync(Guid userId);
    Task CreateAsync(Notification notification);
    Task MarkAsReadAsync(Guid id);
    Task DeleteAsync(Guid id);
}

// SqlNotificationRepository.cs - Production SQL implementation
public class SqlNotificationRepository : INotificationRepository
{
    // Existing implementation unchanged
}

// InMemoryNotificationRepository.cs - For testing
public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = new();

    public Task<Notification> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_notifications.FirstOrDefault(n => n.Id == id));
    }

    public Task<IEnumerable<Notification>> GetUnreadAsync(Guid userId)
    {
        var unread = _notifications.Where(n => n.UserId == userId && !n.IsRead);
        return Task.FromResult(unread);
    }

    public Task CreateAsync(Notification notification)
    {
        _notifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task MarkAsReadAsync(Guid id)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == id);
        if (notification != null) notification.IsRead = true;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _notifications.RemoveAll(n => n.Id == id);
        return Task.CompletedTask;
    }
}

// RedisNotificationRepository.cs - Alternative caching implementation
public class RedisNotificationRepository : INotificationRepository
{
    private readonly IDatabase _redis;

    // Redis-based implementation for high-performance scenarios
}

// DI Registration
services.AddScoped<INotificationRepository, SqlNotificationRepository>(); // Production
// or
services.AddScoped<INotificationRepository, RedisNotificationRepository>(); // High-traffic
// or
services.AddScoped<INotificationRepository, InMemoryNotificationRepository>(); // Testing
```

---

## Example 3: Extract Partial Interface

### Before
```csharp
public class UserManager
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailService _email;

    // Read operations
    public async Task<User> GetByIdAsync(int id) { /* ... */ }
    public async Task<User> GetByEmailAsync(string email) { /* ... */ }
    public async Task<IEnumerable<User>> SearchAsync(string query) { /* ... */ }

    // Write operations
    public async Task<User> CreateAsync(CreateUserRequest request) { /* ... */ }
    public async Task UpdateAsync(User user) { /* ... */ }
    public async Task DeleteAsync(int id) { /* ... */ }

    // Authentication operations
    public async Task<bool> ValidateCredentialsAsync(string email, string password) { /* ... */ }
    public async Task<string> GeneratePasswordResetTokenAsync(int userId) { /* ... */ }
    public async Task ResetPasswordAsync(int userId, string token, string newPassword) { /* ... */ }

    // Admin operations
    public async Task LockAccountAsync(int userId) { /* ... */ }
    public async Task UnlockAccountAsync(int userId) { /* ... */ }
    public async Task<IEnumerable<User>> GetLockedAccountsAsync() { /* ... */ }
}
```

### After (Multiple focused interfaces)
```csharp
// Extract read-only operations for query services
public interface IUserReader
{
    Task<User> GetByIdAsync(int id);
    Task<User> GetByEmailAsync(string email);
    Task<IEnumerable<User>> SearchAsync(string query);
}

// Extract write operations for command handlers
public interface IUserWriter
{
    Task<User> CreateAsync(CreateUserRequest request);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);
}

// Extract authentication for auth services
public interface IUserAuthenticator
{
    Task<bool> ValidateCredentialsAsync(string email, string password);
    Task<string> GeneratePasswordResetTokenAsync(int userId);
    Task ResetPasswordAsync(int userId, string token, string newPassword);
}

// Extract admin operations
public interface IUserAdministration
{
    Task LockAccountAsync(int userId);
    Task UnlockAccountAsync(int userId);
    Task<IEnumerable<User>> GetLockedAccountsAsync();
}

// UserManager implements all interfaces
public class UserManager : IUserReader, IUserWriter, IUserAuthenticator, IUserAdministration
{
    // Implementation unchanged
}

// Consumers only depend on what they need (Interface Segregation Principle)
public class UserProfileController
{
    private readonly IUserReader _userReader;  // Only needs read access

    public UserProfileController(IUserReader userReader)
    {
        _userReader = userReader;
    }

    [HttpGet("{id}")]
    public async Task<User> GetProfile(int id) =>
        await _userReader.GetByIdAsync(id);
}

public class AuthController
{
    private readonly IUserAuthenticator _authenticator;  // Only needs auth

    public AuthController(IUserAuthenticator authenticator)
    {
        _authenticator = authenticator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var isValid = await _authenticator.ValidateCredentialsAsync(
            request.Email, request.Password);
        // ...
    }
}
```

---

## Benefits
1. **Testability**: Interfaces enable mocking in unit tests
2. **Flexibility**: Swap implementations without changing consumers
3. **Interface Segregation**: Clients depend only on methods they use
4. **Dependency Injection**: Interfaces work naturally with DI containers
5. **Documentation**: Interface clearly defines the contract
