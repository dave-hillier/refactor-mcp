namespace Staff
{
    public class Manager : Employee
    {
        public Manager(string name)
        {
            _name = name;
        }

        public decimal Bonus { get; set; }

        public string Payslip() => Badge() + ", bonus " + Bonus;
    }
}
