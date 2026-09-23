namespace Shop
{
    public abstract class Employee
    {
        public string Name;
    }

    public class Salesman : Employee
    {
        public string Describe() => "Employee " + Name;
    }

    public class Engineer : Employee
    {
        public int Level;

        public string Describe() => "Employee " + Name;
    }
}
