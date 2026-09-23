namespace Shop
{
    public static class Payroll
    {
        public static void Pay(Person person)
        {
        }

        public static void PayManager(Manager manager) => Pay(manager);
    }
}
