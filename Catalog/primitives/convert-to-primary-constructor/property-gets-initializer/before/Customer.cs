namespace Shop;

public class Customer
{
    private readonly decimal _limit;

    public Customer(string name, decimal limit)
    {
        Name = name;
        _limit = limit;
    }

    public string Name { get; }

    public bool CanSpend(decimal amount) => amount <= _limit;
}
