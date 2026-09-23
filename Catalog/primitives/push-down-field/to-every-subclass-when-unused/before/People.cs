namespace Shop
{
    public abstract class Employee
    {
        public string Notes = "";

        public abstract decimal Pay();
    }

    public class Salesman : Employee
    {
        public override decimal Pay() => 100;
    }

    public class Engineer : Employee
    {
        public int Level;

        public override decimal Pay() => 200;
    }
}
