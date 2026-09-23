using System;

namespace Bank;

public enum AccountKind
{
    Current,
    Savings
}

public abstract class Account
{
    protected abstract AccountKind Kind { get; }

    protected Account(string owner, string? nickname)
    {
        Owner = owner;
        Nickname = nickname;
    }

    public static Account Create(string owner, AccountKind kind, string? nickname) => kind switch
    {
        AccountKind.Current => new Current(owner, nickname),
        AccountKind.Savings => new Savings(owner, nickname),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public string Owner { get; }

    public string? Nickname { get; }

    public decimal Rate() => Kind == AccountKind.Savings ? 0.02m : 0m;
}

public sealed class Current : Account
{
    public Current(string owner, string? nickname) : base(owner, nickname)
    {
    }

    protected override AccountKind Kind => AccountKind.Current;
}

public sealed class Savings : Account
{
    public Savings(string owner, string? nickname) : base(owner, nickname)
    {
    }

    protected override AccountKind Kind => AccountKind.Savings;
}
