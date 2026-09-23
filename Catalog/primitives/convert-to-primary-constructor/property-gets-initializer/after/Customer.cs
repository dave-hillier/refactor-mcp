namespace Shop;

public class Customer(string name, decimal limit)
{
    public string Name { get; } = name;

    public bool CanSpend(decimal amount) => amount <= limit;
}
