namespace Shop;

public class Customer
{
    public Customer(string name)
    {
        Name = name;
    }

    /// <summary>The name printed on invoices.</summary>
    public string Name { get; }
}
