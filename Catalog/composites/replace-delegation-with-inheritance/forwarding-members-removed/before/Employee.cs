namespace Staff
{
    public class Employee
    {
        // The person this employee is.
        private readonly Person _person = new Person();

        public decimal Salary { get; set; }

        public string Name
        {
            get => _person.Name;
            set => _person.Name = value;
        }

        public string LastName()
        {
            return _person.LastName();
        }

        public string Badge() => _person.Greeting("Employee") + " (" + this._person.LastName() + ")";
    }
}
