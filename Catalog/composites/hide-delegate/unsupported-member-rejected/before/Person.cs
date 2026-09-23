namespace Staff
{
    public class Ledger
    {
        public bool TryBalance(out decimal balance)
        {
            balance = 0;
            return true;
        }
    }

    public class Person
    {
        public Ledger Ledger { get; } = new Ledger();
    }

    public class Audit
    {
        public bool Check(Person person) => person.Ledger.TryBalance(out _);
    }
}
