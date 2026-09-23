namespace Shop
{
    public class Employee
    {
        public string Name;
    }

    public class Manager : Employee
    {
        public Manager(string name)
        {
            Name = name;
        }
    }

    public class Engineer : Employee
    {
    }
}
