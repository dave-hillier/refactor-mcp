namespace Shop
{
    public class Customer
    {
        private string _name = "";

        public string Name
        {
            get { return _name; }
            protected set
            {
                // Names are stored trimmed.
                _name = value.Trim();
            }
        }

        public void Rename(string name) => Name = name;
    }
}
