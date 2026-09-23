namespace Shop
{
    public abstract class Employee
    {
        protected string _name;

        protected Employee(string name)
        {
            // Every employee is named.
            _name = name;
        }
    }

    public class Manager : Employee
    {
        private readonly int _grade;

        public Manager(string name, int grade)
            : base(name)
        {
            // Only managers are graded.
            _grade = grade;
        }
    }
}
