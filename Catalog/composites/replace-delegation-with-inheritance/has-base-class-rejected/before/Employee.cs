namespace Staff
{
    public class Worker
    {
        public decimal Salary { get; set; }
    }

    public class Employee : Worker
    {
        private readonly Person _person = new Person();

        public string LastName() => _person.LastName();
    }
}
