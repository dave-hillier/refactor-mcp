namespace Shop
{
    public class Employee
    {
        public virtual string Describe() => "employee";
    }

    public class Manager : Employee
    {
        public override string Describe() => "manager";
    }
}
