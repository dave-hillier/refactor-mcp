namespace Shop
{
    public static class Payroll
    {
        public static decimal Total(Salesman salesman) => salesman.Salary + salesman.Commission();
    }
}
