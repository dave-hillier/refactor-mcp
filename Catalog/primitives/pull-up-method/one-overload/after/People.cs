namespace Shop
{
    public class Employee
    {
        public decimal Paid;

        public void Pay(decimal amount)
        {
            Paid += amount;
        }
    }

    public class Manager : Employee
    {
        public int Grade;

        public void Pay() => Pay(Grade * 1000m);
    }
}
