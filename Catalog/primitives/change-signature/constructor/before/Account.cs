namespace Shop;

public class Account
{
    private readonly string _owner;

    public Account(string owner)
    {
        _owner = owner;
    }

    public Account() : this("guest")
    {
    }

    public string Owner => _owner;
}

public class Savings : Account
{
    public Savings(string owner) : base(owner)
    {
    }
}
