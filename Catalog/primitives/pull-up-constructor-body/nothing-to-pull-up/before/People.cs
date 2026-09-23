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
            _grade = grade;
            _name = name;
        }
    }
}
