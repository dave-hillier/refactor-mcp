namespace Shop
{
    public abstract class Employee
    {
        protected string _name;

        protected Employee()
        {
        }

        protected Employee(string name)
        {
            _name = name.ToUpperInvariant();
        }
    }

    public class Manager : Employee
    {
        public Manager(string name)
        {
            _name = name;
        }
    }
}
