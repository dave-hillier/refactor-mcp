namespace ECommerce;

/// <summary>
/// Customer management with dependency and interface issues.
///
/// Refactoring opportunities:
///   - use-interface: UpdateCustomerTier takes concrete CustomerRepository, should take ICustomerRepository
///   - constructor-injection: RegisterCustomer takes EmailService as method param, should be injected
///   - make-field-readonly: _repository is set only in constructor
///   - introduce-parameter: hardcoded tier thresholds in CalculateTierUpgrade
///   - safe-delete-parameter: unused 'verbose' parameter in GetCustomerSummary
/// </summary>
public class CustomerService
{
    private CustomerRepository _repository;
    private readonly EmailService _emailService;

    public CustomerService(CustomerRepository repository, EmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    /// <summary>
    /// Takes a concrete CustomerRepository — should use ICustomerRepository interface.
    /// </summary>
    public bool UpdateCustomerTier(Customer customer, CustomerRepository repository)
    {
        var updatedTier = CalculateTierUpgrade(customer);
        if (updatedTier != customer.Tier)
        {
            customer.Tier = updatedTier;
            repository.Save(customer);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Has hardcoded tier thresholds — introduce-parameter candidates.
    /// </summary>
    public CustomerTier CalculateTierUpgrade(Customer customer)
    {
        var yearsActive = (DateTime.UtcNow - customer.MemberSince).TotalDays / 365.0;

        if (customer.LifetimeSpend >= 25000m && yearsActive >= 2)
            return CustomerTier.Platinum;
        if (customer.LifetimeSpend >= 10000m && yearsActive >= 1)
            return CustomerTier.Gold;
        if (customer.LifetimeSpend >= 2500m)
            return CustomerTier.Silver;

        return CustomerTier.Standard;
    }

    /// <summary>
    /// EmailService is passed as a method parameter — should be constructor-injected.
    /// </summary>
    public void RegisterCustomer(string name, string email, EmailService emailService)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Email = email,
            MemberSince = DateTime.UtcNow
        };

        _repository.Save(customer);
        emailService.Send(email, "Welcome!", $"Welcome to our store, {name}!");
    }

    /// <summary>
    /// The 'verbose' parameter is never actually used — safe-delete-parameter candidate.
    /// </summary>
    public string GetCustomerSummary(Customer customer, bool verbose)
    {
        return $"{customer.Name} | Tier: {customer.Tier} | Lifetime: {customer.LifetimeSpend:C} | Member since: {customer.MemberSince:yyyy-MM-dd}";
    }

    public List<Customer> GetTopCustomers(int count)
    {
        return _repository.GetAll()
            .OrderByDescending(c => c.LifetimeSpend)
            .Take(count)
            .ToList();
    }
}

// ── Dependencies (concrete classes that should have interfaces) ────

public interface ICustomerRepository
{
    void Save(Customer customer);
    Customer? FindById(string id);
    List<Customer> GetAll();
}

public class CustomerRepository : ICustomerRepository
{
    private readonly Dictionary<string, Customer> _store = new();

    public void Save(Customer customer)
    {
        _store[customer.Id] = customer;
    }

    public Customer? FindById(string id)
    {
        return _store.GetValueOrDefault(id);
    }

    public List<Customer> GetAll()
    {
        return _store.Values.ToList();
    }
}

public class EmailService
{
    private readonly List<(string To, string Subject, string Body)> _sent = new();

    public void Send(string to, string subject, string body)
    {
        _sent.Add((to, subject, body));
        Console.WriteLine($"[Email] To: {to} | Subject: {subject}");
    }

    public int GetSentCount() => _sent.Count;
}
