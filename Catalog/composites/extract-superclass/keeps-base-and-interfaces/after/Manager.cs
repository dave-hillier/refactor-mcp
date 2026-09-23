namespace Staff
{
    public interface IPayable
    {
        decimal Pay();
    }

    public class Person
    {
        public string Name { get; set; } = "";
    }

    // Managers are paid monthly.
    public class Manager : Employee, IPayable
    {
        public decimal Pay() => _salary / 12;
    }
}
