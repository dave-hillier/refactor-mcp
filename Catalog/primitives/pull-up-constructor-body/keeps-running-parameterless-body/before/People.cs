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
    }

    public class Manager : Employee
    {
        private readonly int _grade;

        public Manager(string name, int grade)
        {
            _name = name;
            _grade = grade;
        }
    }
}
