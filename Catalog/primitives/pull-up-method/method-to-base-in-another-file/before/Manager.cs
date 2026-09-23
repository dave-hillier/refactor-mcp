namespace Shop
{
    public class Manager : Employee
    {
        public int Grade;

        public string Describe()
        {
            return Name + " earns " + Salary;
        }

        public bool IsSenior() => Grade > 2;
    }
}
