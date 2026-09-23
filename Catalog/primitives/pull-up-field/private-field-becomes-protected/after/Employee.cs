namespace Shop
{
    public class Employee
    {
        public decimal Salary;
        protected string _name;

        public decimal Monthly() => Salary / 12;
    }
}
