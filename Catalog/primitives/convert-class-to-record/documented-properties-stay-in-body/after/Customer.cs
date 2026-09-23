namespace Shop;

public record Customer
{
    public Customer(string name)
    {
        Name = name;
    }

    /// <summary>The name printed on invoices.</summary>
    public string Name { get; }
}
