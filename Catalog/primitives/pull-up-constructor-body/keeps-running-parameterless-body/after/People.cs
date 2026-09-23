namespace Shop
{
    public abstract class Employee
    {
        protected bool _active;
        protected string _name;

        protected Employee()
        {
            _active = true;
        }

        protected Employee(string name)
            : this()
        {
            _name = name;
        }
    }

    public class Manager : Employee
    {
        private readonly int _grade;

        public Manager(string name, int grade)
            : base(name)
        {
            _grade = grade;
        }
    }
}
