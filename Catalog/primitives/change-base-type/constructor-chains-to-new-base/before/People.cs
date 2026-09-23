namespace Shop
{
    public class Person
    {
        public Person(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public class Employee : Person
    {
        public Employee(string name)
            : base(name)
        {
        }
    }

    public class Manager : Person
    {
        public Manager(string name)
            : base(name)
        {
        }
    }
}
