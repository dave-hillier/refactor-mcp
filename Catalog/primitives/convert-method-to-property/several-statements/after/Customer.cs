namespace Shop
{
    public class Customer
    {
        private string _name = "";

        public string Summary
        {
            get
            {
                // Names are stored untrimmed.
                var text = _name.Trim();
                return text.ToUpperInvariant();
            }
        }

        public void Rename(string name) => _name = name;
    }
}
