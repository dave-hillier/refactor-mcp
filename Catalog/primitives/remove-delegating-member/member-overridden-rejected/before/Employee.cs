namespace Staff
{
    public class Employee : Person
    {
        public new virtual string LastName() => base.LastName();
    }

    public class Manager : Employee
    {
        public override string LastName() => "Manager " + base.LastName();
    }
}
