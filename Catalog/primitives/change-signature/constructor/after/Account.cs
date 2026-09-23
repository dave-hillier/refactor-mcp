namespace Shop;

public class Account
{
    private readonly string _owner;

    public Account(string owner, decimal limit)
    {
        _owner = owner;
    }

    public Account() : this("guest", 0m)
    {
    }

    public string Owner => _owner;
}

public class Savings : Account
{
    public Savings(string owner) : base(owner, 0m)
    {
    }
}
