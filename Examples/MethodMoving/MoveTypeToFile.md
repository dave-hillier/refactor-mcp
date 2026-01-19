# Move Type to File Refactoring

## Overview
The `move-type-to-file` refactoring moves a top-level type (class, struct, interface, enum, record) from a file that contains multiple types into its own dedicated file.

## When to Use
- When a file contains multiple types and is getting hard to navigate
- When following the "one type per file" convention
- When a nested type has grown large enough to warrant its own file
- When preparing to expose a previously internal type

---

## Example 1: Separate Multiple Domain Models

### Before (Models.cs - 200+ lines with multiple models)
```csharp
// Models.cs - Multiple related but distinct types in one file
namespace ECommerce.Domain.Models
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public OrderStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public ShippingAddress ShippingAddress { get; set; }
        public BillingAddress BillingAddress { get; set; }
        public PaymentInfo Payment { get; set; }

        public void AddItem(Product product, int quantity)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductId == product.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = quantity
                });
            }
            RecalculateTotals();
        }

        public void RecalculateTotals()
        {
            Subtotal = Items.Sum(i => i.LineTotal);
            Tax = Subtotal * 0.08m;
            Total = Subtotal + Tax + ShippingCost;
        }
    }

    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class Customer
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public DateTime CreatedAt { get; set; }
        public CustomerTier Tier { get; set; }
        public List<Order> Orders { get; set; } = new();
        public List<Address> Addresses { get; set; } = new();

        public decimal GetTotalSpent() => Orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Sum(o => o.Total);
    }

    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Sku { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockQuantity { get; set; }
        public ProductCategory Category { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public decimal Margin => Price > 0 ? (Price - Cost) / Price : 0;
        public bool IsInStock => StockQuantity > 0;
    }

    public abstract class Address
    {
        public Guid Id { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

        public string FormatOneLine() =>
            $"{Street}, {City}, {State} {PostalCode}, {Country}";
    }

    public class ShippingAddress : Address
    {
        public string RecipientName { get; set; }
        public string PhoneNumber { get; set; }
        public string DeliveryInstructions { get; set; }
    }

    public class BillingAddress : Address
    {
        public string CardholderName { get; set; }
    }

    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Processing,
        Shipped,
        Delivered,
        Cancelled,
        Refunded
    }

    public enum CustomerTier
    {
        Standard,
        Silver,
        Gold,
        Platinum
    }

    public enum ProductCategory
    {
        Electronics,
        Clothing,
        Home,
        Sports,
        Books,
        Other
    }

    public record PaymentInfo(
        string CardType,
        string LastFourDigits,
        DateTime ExpirationDate,
        string TransactionId);
}
```

### After (Separate files for each type)
```csharp
// Order.cs
namespace ECommerce.Domain.Models
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public OrderStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public ShippingAddress ShippingAddress { get; set; }
        public BillingAddress BillingAddress { get; set; }
        public PaymentInfo Payment { get; set; }

        public void AddItem(Product product, int quantity)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductId == product.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = quantity
                });
            }
            RecalculateTotals();
        }

        public void RecalculateTotals()
        {
            Subtotal = Items.Sum(i => i.LineTotal);
            Tax = Subtotal * 0.08m;
            Total = Subtotal + Tax + ShippingCost;
        }
    }
}

// OrderItem.cs
namespace ECommerce.Domain.Models
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }
}

// Customer.cs
namespace ECommerce.Domain.Models
{
    public class Customer
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public DateTime CreatedAt { get; set; }
        public CustomerTier Tier { get; set; }
        public List<Order> Orders { get; set; } = new();
        public List<Address> Addresses { get; set; } = new();

        public decimal GetTotalSpent() => Orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Sum(o => o.Total);
    }
}

// Product.cs
namespace ECommerce.Domain.Models
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Sku { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockQuantity { get; set; }
        public ProductCategory Category { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public decimal Margin => Price > 0 ? (Price - Cost) / Price : 0;
        public bool IsInStock => StockQuantity > 0;
    }
}

// Address.cs
namespace ECommerce.Domain.Models
{
    public abstract class Address
    {
        public Guid Id { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

        public string FormatOneLine() =>
            $"{Street}, {City}, {State} {PostalCode}, {Country}";
    }
}

// ShippingAddress.cs
namespace ECommerce.Domain.Models
{
    public class ShippingAddress : Address
    {
        public string RecipientName { get; set; }
        public string PhoneNumber { get; set; }
        public string DeliveryInstructions { get; set; }
    }
}

// BillingAddress.cs
namespace ECommerce.Domain.Models
{
    public class BillingAddress : Address
    {
        public string CardholderName { get; set; }
    }
}

// OrderStatus.cs
namespace ECommerce.Domain.Models
{
    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Processing,
        Shipped,
        Delivered,
        Cancelled,
        Refunded
    }
}

// CustomerTier.cs
namespace ECommerce.Domain.Models
{
    public enum CustomerTier
    {
        Standard,
        Silver,
        Gold,
        Platinum
    }
}

// ProductCategory.cs
namespace ECommerce.Domain.Models
{
    public enum ProductCategory
    {
        Electronics,
        Clothing,
        Home,
        Sports,
        Books,
        Other
    }
}

// PaymentInfo.cs
namespace ECommerce.Domain.Models
{
    public record PaymentInfo(
        string CardType,
        string LastFourDigits,
        DateTime ExpirationDate,
        string TransactionId);
}
```

### Tool Usage
```bash
# Move each type one at a time
dotnet run --project RefactorMCP.ConsoleApp -- --json move-type-to-file '{
    "solutionPath": "ECommerce.sln",
    "sourceFilePath": "Domain/Models.cs",
    "typeName": "Order"
}'

dotnet run --project RefactorMCP.ConsoleApp -- --json move-type-to-file '{
    "solutionPath": "ECommerce.sln",
    "sourceFilePath": "Domain/Models.cs",
    "typeName": "Customer"
}'
```

---

## Example 2: Separate Exception Types

### Before (Exceptions.cs)
```csharp
// Exceptions.cs - All custom exceptions in one file
namespace MyApp.Exceptions
{
    public class EntityNotFoundException : Exception
    {
        public string EntityType { get; }
        public object EntityId { get; }

        public EntityNotFoundException(string entityType, object entityId)
            : base($"{entityType} with ID '{entityId}' was not found.")
        {
            EntityType = entityType;
            EntityId = entityId;
        }

        public EntityNotFoundException(string entityType, object entityId, Exception innerException)
            : base($"{entityType} with ID '{entityId}' was not found.", innerException)
        {
            EntityType = entityType;
            EntityId = entityId;
        }
    }

    public class ValidationException : Exception
    {
        public IReadOnlyList<ValidationError> Errors { get; }

        public ValidationException(IEnumerable<ValidationError> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors.ToList().AsReadOnly();
        }

        public ValidationException(string propertyName, string errorMessage)
            : base($"Validation failed for {propertyName}: {errorMessage}")
        {
            Errors = new List<ValidationError>
            {
                new ValidationError(propertyName, errorMessage)
            }.AsReadOnly();
        }
    }

    public record ValidationError(string PropertyName, string Message);

    public class ConcurrencyException : Exception
    {
        public string EntityType { get; }
        public object EntityId { get; }
        public object ExpectedVersion { get; }
        public object ActualVersion { get; }

        public ConcurrencyException(
            string entityType,
            object entityId,
            object expectedVersion,
            object actualVersion)
            : base($"Concurrency conflict for {entityType} '{entityId}'. " +
                   $"Expected version {expectedVersion}, but found {actualVersion}.")
        {
            EntityType = entityType;
            EntityId = entityId;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }
    }

    public class AuthorizationException : Exception
    {
        public string UserId { get; }
        public string Resource { get; }
        public string Action { get; }

        public AuthorizationException(string userId, string resource, string action)
            : base($"User '{userId}' is not authorized to {action} on {resource}.")
        {
            UserId = userId;
            Resource = resource;
            Action = action;
        }
    }

    public class BusinessRuleException : Exception
    {
        public string RuleName { get; }

        public BusinessRuleException(string ruleName, string message)
            : base(message)
        {
            RuleName = ruleName;
        }
    }
}
```

### After (Separate files)
```csharp
// EntityNotFoundException.cs
namespace MyApp.Exceptions
{
    public class EntityNotFoundException : Exception
    {
        public string EntityType { get; }
        public object EntityId { get; }

        public EntityNotFoundException(string entityType, object entityId)
            : base($"{entityType} with ID '{entityId}' was not found.")
        {
            EntityType = entityType;
            EntityId = entityId;
        }

        public EntityNotFoundException(string entityType, object entityId, Exception innerException)
            : base($"{entityType} with ID '{entityId}' was not found.", innerException)
        {
            EntityType = entityType;
            EntityId = entityId;
        }
    }
}

// ValidationException.cs
namespace MyApp.Exceptions
{
    public class ValidationException : Exception
    {
        public IReadOnlyList<ValidationError> Errors { get; }

        public ValidationException(IEnumerable<ValidationError> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors.ToList().AsReadOnly();
        }

        public ValidationException(string propertyName, string errorMessage)
            : base($"Validation failed for {propertyName}: {errorMessage}")
        {
            Errors = new List<ValidationError>
            {
                new ValidationError(propertyName, errorMessage)
            }.AsReadOnly();
        }
    }
}

// ValidationError.cs
namespace MyApp.Exceptions
{
    public record ValidationError(string PropertyName, string Message);
}

// ConcurrencyException.cs
namespace MyApp.Exceptions
{
    public class ConcurrencyException : Exception
    {
        public string EntityType { get; }
        public object EntityId { get; }
        public object ExpectedVersion { get; }
        public object ActualVersion { get; }

        public ConcurrencyException(
            string entityType,
            object entityId,
            object expectedVersion,
            object actualVersion)
            : base($"Concurrency conflict for {entityType} '{entityId}'. " +
                   $"Expected version {expectedVersion}, but found {actualVersion}.")
        {
            EntityType = entityType;
            EntityId = entityId;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }
    }
}

// AuthorizationException.cs
namespace MyApp.Exceptions
{
    public class AuthorizationException : Exception
    {
        public string UserId { get; }
        public string Resource { get; }
        public string Action { get; }

        public AuthorizationException(string userId, string resource, string action)
            : base($"User '{userId}' is not authorized to {action} on {resource}.")
        {
            UserId = userId;
            Resource = resource;
            Action = action;
        }
    }
}

// BusinessRuleException.cs
namespace MyApp.Exceptions
{
    public class BusinessRuleException : Exception
    {
        public string RuleName { get; }

        public BusinessRuleException(string ruleName, string message)
            : base(message)
        {
            RuleName = ruleName;
        }
    }
}
```

---

## Benefits
1. **Navigation**: Each type is easy to find in file explorer
2. **Version Control**: Changes to one type show clear diffs
3. **Parallel Editing**: Multiple developers can edit different types simultaneously
4. **Convention Compliance**: Follows common "one type per file" standard
5. **Reduced Merge Conflicts**: File-per-type reduces conflicts in team development
