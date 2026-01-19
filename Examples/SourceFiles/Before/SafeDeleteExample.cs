using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Examples.SafeDelete;

/// <summary>
/// Example: UserService with unused fields, methods, and parameters that can be safely deleted.
/// Refactoring: safe-delete-field, safe-delete-method, safe-delete-parameter
/// </summary>
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    // UNUSED FIELDS - can be safely deleted
    private readonly ILegacyAuthProvider _legacyAuth;      // Never referenced
    private readonly Dictionary<int, User> _userCache;     // Never referenced
    private readonly int _maxCacheSize = 1000;             // Never referenced
    private DateTime _lastCacheCleanup;                    // Only assigned, never read

    public UserService(
        IUserRepository userRepository,
        IEmailService emailService,
        ILegacyAuthProvider legacyAuth)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _legacyAuth = legacyAuth;
        _userCache = new Dictionary<int, User>();
    }

    public async Task<User> GetUserAsync(int id)
    {
        _lastCacheCleanup = DateTime.UtcNow;  // Assigned but never read
        return await _userRepository.GetByIdAsync(id);
    }

    public async Task CreateUserAsync(User user)
    {
        await _userRepository.CreateAsync(user);
        await _emailService.SendWelcomeAsync(user.Email);
    }

    // UNUSED METHOD - can be safely deleted
    public string FormatUserLegacy(User user)
    {
        return $"USER-{user.Id:D8}";
    }

    // UNUSED METHOD - can be safely deleted
    private bool IsUserCacheValid()
    {
        return _userCache.Count < _maxCacheSize;
    }

    // UNUSED PARAMETER - 'priority' is never used
    public void SendNotification(int userId, string message, NotificationPriority priority)
    {
        // Note: priority parameter is never used!
        Console.WriteLine($"Sending to user {userId}: {message}");
    }
}

// Supporting types
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
}

public enum NotificationPriority
{
    Low,
    Normal,
    High
}

public interface IUserRepository
{
    Task<User> GetByIdAsync(int id);
    Task CreateAsync(User user);
}

public interface IEmailService
{
    Task SendWelcomeAsync(string email);
}

public interface ILegacyAuthProvider
{
    bool Authenticate(string username, string password);
}
