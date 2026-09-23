namespace Staff
{
    public class Manager
    {
        private string _name;

        public Manager(string name)
        {
            _name = name;
        }

        public decimal Bonus { get; set; }

        /// <summary>The name as printed on a badge.</summary>
        public string Badge()
        {
            return "Name: " + _name;
        }

        public string Payslip() => Badge() + ", bonus " + Bonus;
    }
}
