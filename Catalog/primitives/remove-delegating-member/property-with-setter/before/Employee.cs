namespace Staff
{
    public class Employee : Person
    {
        public decimal Salary { get; set; }

        public new string Name
        {
            get => base.Name;
            set => base.Name = value;
        }

        public string Badge() => "Employee " + Name;
    }
}
