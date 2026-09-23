namespace Bank;

public enum AccountKind
{
    Current,
    Savings,
    Fixed
}

public abstract class Account
{
    protected abstract AccountKind Kind { get; }

    public virtual decimal Rate() => 0m;
}

public sealed class Current : Account
{
    protected override AccountKind Kind => AccountKind.Current;
}

public sealed class Savings : Account
{
    protected override AccountKind Kind => AccountKind.Savings;

    public override decimal Rate() => 0.02m;
}

public sealed class Fixed : Account
{
    protected override AccountKind Kind => AccountKind.Fixed;

    public override decimal Rate()
    {
        var bonus = 0.01m;
        return 0.03m + bonus;
    }
}
