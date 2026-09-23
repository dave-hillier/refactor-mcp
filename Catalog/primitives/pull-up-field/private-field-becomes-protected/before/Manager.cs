namespace Shop
{
    public class Manager : Employee
    {
        private string _name;

        public string Describe() => "Manager " + _name;
    }
}
