namespace Shop;

public class Customer(string name, decimal limit = 100m)
{
    private int _orders = 0;

    public string Name { get; } = name.Trim();

    public bool CanSpend(decimal amount) => _orders < 10 && amount <= limit;
}
