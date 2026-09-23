namespace Shop
{
    public class Customer
    {
        private string _name = "unknown";
        private int _visits;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public int Visits => _visits;
    }
}
