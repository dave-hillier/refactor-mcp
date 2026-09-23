namespace Shop
{
    public abstract class Employee
    {
        protected string _name;
        protected string _id;

        protected Employee(string name, string id)
        {
            _name = name;
            _id = id;
        }

        public string Describe() => _id + " " + _name;
    }
}
