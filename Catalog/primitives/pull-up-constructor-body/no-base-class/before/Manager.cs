namespace Shop
{
    public class Manager
    {
        private readonly string _name;

        public Manager(string name)
        {
            _name = name;
        }

        public string Name => _name;
    }
}
