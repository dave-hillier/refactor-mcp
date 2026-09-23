namespace Staff
{
    public class Employee : Person
    {
        public decimal Salary { get; set; }

        public string Name
        {
            get => base.Name;
            set => base.Name = value;
        }

        public string LastName()
        {
            return base.LastName();
        }

        public string Badge() => Greeting("Employee") + " (" + base.LastName() + ")";
    }
}
