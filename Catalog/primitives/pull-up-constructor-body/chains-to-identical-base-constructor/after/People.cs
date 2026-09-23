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
            _name = name;
        }
    }

    public class Engineer : Employee
    {
        private readonly int _level;

        public Engineer(string name, int level)
            : base(name)
        {
            _level = level;
        }
    }
}
