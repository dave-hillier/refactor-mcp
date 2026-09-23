namespace Shop
{
    public abstract class Employee
    {
        public abstract decimal Pay();
    }

    public class Salesman : Employee
    {
        public string Notes = "";

        public override decimal Pay() => 100;
    }

    public class Engineer : Employee
    {
        public int Level;
        public string Notes = "";

        public override decimal Pay() => 200;
    }
}
