namespace Shop
{
    public abstract class Employee
    {
        protected string _name;
        protected int _grade;

        protected Employee(string name)
        {
            _name = name;
        }
    }

    public class Manager : Employee
    {
        public Manager(string name, int grade)
            : base(name)
        {
            _grade = grade;
        }
    }
}
