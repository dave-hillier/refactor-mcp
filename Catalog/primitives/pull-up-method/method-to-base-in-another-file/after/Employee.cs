namespace Shop
{
    public class Employee
    {
        public string Name;

        public decimal Salary;

        public string Describe()
        {
            return Name + " earns " + Salary;
        }
    }
}
