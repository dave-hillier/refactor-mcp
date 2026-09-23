namespace Shop
{
    public abstract class Employee
    {
        protected string _name;
    }

    public class Manager : Employee
    {
        private readonly int _grade;

        public Manager(string name, int grade)
        {
            // Every employee is named.
            _name = name;

            // Only managers are graded.
            _grade = grade;
        }
    }
}
