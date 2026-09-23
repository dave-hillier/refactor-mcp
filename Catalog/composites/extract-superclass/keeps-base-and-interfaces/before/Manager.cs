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
    public class Manager : Person, IPayable
    {
        private decimal _salary = 60000m;

        public decimal Pay() => _salary / 12;

        public string Greeting() => "Hello, " + Name;
    }
}
