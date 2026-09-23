namespace Shop
{
    public class Employee
    {
        public string Name;

        public Employee()
        {
        }

        protected Employee(string name)
        {
            Name = name;
        }
    }

    public class Manager : Employee
    {
        public Manager(string name)
            : base(name)
        {
        }
    }

    public class Engineer : Employee
    {
    }
}
