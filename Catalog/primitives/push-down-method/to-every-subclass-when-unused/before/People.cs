namespace Shop
{
    public abstract class Employee
    {
        public string Name;

        public string Describe() => "Employee " + Name;
    }

    public class Salesman : Employee
    {
    }

    public class Engineer : Employee
    {
        public int Level;
    }
}
