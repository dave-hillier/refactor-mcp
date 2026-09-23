namespace Shop
{
    public abstract class Employee
    {
        protected string _name;
        protected string _id;

        public string Describe() => _id + " " + _name;
    }
}
