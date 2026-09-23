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

    public decimal Rate()
    {
        if (Kind == AccountKind.Savings)
            return 0.02m;
        if (AccountKind.Fixed == Kind)
        {
            var bonus = 0.01m;
            return 0.03m + bonus;
        }

        return 0m;
    }
}

public sealed class Current : Account
{
    protected override AccountKind Kind => AccountKind.Current;
}

public sealed class Savings : Account
{
    protected override AccountKind Kind => AccountKind.Savings;
}

public sealed class Fixed : Account
{
    protected override AccountKind Kind => AccountKind.Fixed;
}
