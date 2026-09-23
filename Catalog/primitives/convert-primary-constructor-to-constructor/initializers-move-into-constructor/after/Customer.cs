namespace Shop;

public class Customer
{
    private readonly decimal _limit;
    private int _orders = 0;

    public Customer(string name, decimal limit = 100m)
    {
        _limit = limit;
        Name = name.Trim();
    }

    public string Name { get; }

    public bool CanSpend(decimal amount) => _orders < 10 && amount <= _limit;
}
