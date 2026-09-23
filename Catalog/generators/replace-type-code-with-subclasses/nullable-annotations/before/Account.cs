namespace Bank;

public enum AccountKind
{
    Current,
    Savings
}

public class Account
{
    private readonly AccountKind _kind;

    public Account(string owner, AccountKind kind, string? nickname)
    {
        Owner = owner;
        _kind = kind;
        Nickname = nickname;
    }

    public string Owner { get; }

    public string? Nickname { get; }

    public decimal Rate() => _kind == AccountKind.Savings ? 0.02m : 0m;
}
