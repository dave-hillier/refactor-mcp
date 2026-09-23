namespace Shop
{
    public class Employee
    {
        public decimal Paid;
    }

    public class Manager : Employee
    {
        public int Grade;

        public void Pay() => Pay(Grade * 1000m);

        public void Pay(decimal amount)
        {
            Paid += amount;
        }
    }
}
