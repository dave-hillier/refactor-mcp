# Move Instance Method Refactoring

## Overview
The `move-instance-method` refactoring moves one or more instance methods from one class to another. It handles dependency injection, creates wrapper methods to preserve the API, and auto-creates target classes when needed.

## When to Use
- When a method uses more features of another class than its own (Feature Envy)
- When separating concerns into different classes
- When refactoring toward better cohesion
- When extracting domain logic from services or controllers

---

## Example 1: Move Feature Envy Method

### Before
```csharp
// OrderService.cs - Method has feature envy toward PricingCalculator
public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(OrderRequest request)
    {
        var order = new Order
        {
            CustomerId = request.CustomerId,
            Items = request.Items,
            CreatedAt = DateTime.UtcNow
        };

        // This calculation logic belongs in a PricingCalculator
        order.Subtotal = CalculateSubtotal(order.Items);
        order.Tax = CalculateTax(order.Subtotal, request.ShippingAddress.State);
        order.ShippingCost = CalculateShipping(order.Items, request.ShippingAddress);
        order.Total = order.Subtotal + order.Tax + order.ShippingCost;

        await _orderRepository.SaveAsync(order);
        return order;
    }

    // These methods are all about pricing - they should be moved
    private decimal CalculateSubtotal(List<OrderItem> items)
    {
        return items.Sum(item => item.UnitPrice * item.Quantity);
    }

    private decimal CalculateTax(decimal subtotal, string state)
    {
        var taxRates = new Dictionary<string, decimal>
        {
            { "CA", 0.0725m },
            { "NY", 0.08m },
            { "TX", 0.0625m },
            { "WA", 0.065m }
        };

        var rate = taxRates.GetValueOrDefault(state, 0.05m);
        return Math.Round(subtotal * rate, 2);
    }

    private decimal CalculateShipping(List<OrderItem> items, Address address)
    {
        var totalWeight = items.Sum(i => i.Weight * i.Quantity);

        decimal baseRate = address.Country == "US" ? 5.99m : 19.99m;
        decimal weightCharge = totalWeight * 0.50m;

        if (items.Sum(i => i.UnitPrice * i.Quantity) > 100)
        {
            return 0; // Free shipping over $100
        }

        return Math.Round(baseRate + weightCharge, 2);
    }
}
```

### After
```csharp
// OrderService.cs - Now focused on order orchestration
public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<OrderService> _logger;
    private readonly PricingCalculator _pricingCalculator;

    public OrderService(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        ILogger<OrderService> logger,
        PricingCalculator pricingCalculator)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _logger = logger;
        _pricingCalculator = pricingCalculator;
    }

    public async Task<Order> CreateOrderAsync(OrderRequest request)
    {
        var order = new Order
        {
            CustomerId = request.CustomerId,
            Items = request.Items,
            CreatedAt = DateTime.UtcNow
        };

        order.Subtotal = _pricingCalculator.CalculateSubtotal(order.Items);
        order.Tax = _pricingCalculator.CalculateTax(order.Subtotal, request.ShippingAddress.State);
        order.ShippingCost = _pricingCalculator.CalculateShipping(order.Items, request.ShippingAddress);
        order.Total = order.Subtotal + order.Tax + order.ShippingCost;

        await _orderRepository.SaveAsync(order);
        return order;
    }
}

// PricingCalculator.cs - New class with focused pricing responsibility
public class PricingCalculator
{
    public decimal CalculateSubtotal(List<OrderItem> items)
    {
        return items.Sum(item => item.UnitPrice * item.Quantity);
    }

    public decimal CalculateTax(decimal subtotal, string state)
    {
        var taxRates = new Dictionary<string, decimal>
        {
            { "CA", 0.0725m },
            { "NY", 0.08m },
            { "TX", 0.0625m },
            { "WA", 0.065m }
        };

        var rate = taxRates.GetValueOrDefault(state, 0.05m);
        return Math.Round(subtotal * rate, 2);
    }

    public decimal CalculateShipping(List<OrderItem> items, Address address)
    {
        var totalWeight = items.Sum(i => i.Weight * i.Quantity);

        decimal baseRate = address.Country == "US" ? 5.99m : 19.99m;
        decimal weightCharge = totalWeight * 0.50m;

        if (items.Sum(i => i.UnitPrice * i.Quantity) > 100)
        {
            return 0; // Free shipping over $100
        }

        return Math.Round(baseRate + weightCharge, 2);
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-instance-method '{
    "solutionPath": "MyProject.sln",
    "sourceFilePath": "Services/OrderService.cs",
    "methodNames": ["CalculateSubtotal", "CalculateTax", "CalculateShipping"],
    "targetClassName": "PricingCalculator",
    "targetFilePath": "Services/PricingCalculator.cs"
}'
```

---

## Example 2: Extract Domain Logic from Controller

### Before
```csharp
// UserController.cs - Too much domain logic in controller
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserController> _logger;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        // Validation logic that belongs in domain
        if (!IsValidEmail(request.Email))
        {
            return BadRequest("Invalid email format");
        }

        if (!IsStrongPassword(request.Password))
        {
            return BadRequest("Password does not meet requirements");
        }

        // Business logic that should be in a service
        var existingUser = await _userRepository.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Conflict("User already exists");
        }

        var passwordHash = HashPassword(request.Password);
        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            VerificationToken = GenerateVerificationToken()
        };

        await _userRepository.CreateAsync(user);
        await SendVerificationEmailAsync(user);

        return Ok(new { UserId = user.Id });
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private bool IsStrongPassword(string password)
    {
        return password.Length >= 8
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
    }

    private string HashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var salt = Guid.NewGuid().ToString();
        var saltedPassword = password + salt;
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
        return Convert.ToBase64String(hash) + ":" + salt;
    }

    private string GenerateVerificationToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private async Task SendVerificationEmailAsync(User user)
    {
        var verificationLink = $"https://myapp.com/verify?token={user.VerificationToken}";
        await _emailService.SendAsync(
            user.Email,
            "Verify Your Email",
            $"Click here to verify: {verificationLink}");
    }
}
```

### After
```csharp
// UserController.cs - Now thin, orchestrating only
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly UserRegistrationService _registrationService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        UserRegistrationService registrationService,
        ILogger<UserController> logger)
    {
        _registrationService = registrationService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        var result = await _registrationService.RegisterAsync(request);

        return result.Match<IActionResult>(
            success => Ok(new { UserId = success.UserId }),
            validationError => BadRequest(validationError.Message),
            conflictError => Conflict(conflictError.Message)
        );
    }
}

// UserRegistrationService.cs - Domain logic extracted
public class UserRegistrationService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly UserValidation _validation;
    private readonly PasswordHasher _passwordHasher;

    public UserRegistrationService(
        IUserRepository userRepository,
        IEmailService emailService,
        UserValidation validation,
        PasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _validation = validation;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest request)
    {
        if (!_validation.IsValidEmail(request.Email))
        {
            return RegistrationResult.ValidationError("Invalid email format");
        }

        if (!_validation.IsStrongPassword(request.Password))
        {
            return RegistrationResult.ValidationError("Password does not meet requirements");
        }

        var existingUser = await _userRepository.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return RegistrationResult.Conflict("User already exists");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            VerificationToken = GenerateVerificationToken()
        };

        await _userRepository.CreateAsync(user);
        await SendVerificationEmailAsync(user);

        return RegistrationResult.Success(user.Id);
    }

    private string GenerateVerificationToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private async Task SendVerificationEmailAsync(User user)
    {
        var verificationLink = $"https://myapp.com/verify?token={user.VerificationToken}";
        await _emailService.SendAsync(
            user.Email,
            "Verify Your Email",
            $"Click here to verify: {verificationLink}");
    }
}

// UserValidation.cs - Validation extracted
public class UserValidation
{
    public bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public bool IsStrongPassword(string password)
    {
        return password.Length >= 8
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
    }
}

// PasswordHasher.cs - Security logic extracted
public class PasswordHasher
{
    public string Hash(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var salt = Guid.NewGuid().ToString();
        var saltedPassword = password + salt;
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
        return Convert.ToBase64String(hash) + ":" + salt;
    }
}
```

---

## Benefits
1. **Single Responsibility**: Each class has one reason to change
2. **Testability**: Domain logic can be unit tested without HTTP concerns
3. **Reusability**: Extracted classes can be used from multiple places
4. **Maintainability**: Changes to pricing/validation/hashing are isolated
5. **Cohesion**: Related functionality is grouped together
