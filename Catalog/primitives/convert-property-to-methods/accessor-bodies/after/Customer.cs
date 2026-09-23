namespace Shop
{
    public class Customer
    {
        private string _name = "";

        public string GetName()
        {
            return _name;
        }

        protected void SetName(string value)
        {
            // Names are stored trimmed.
            _name = value.Trim();
        }

        public void Rename(string name) => SetName(name);
    }
}
