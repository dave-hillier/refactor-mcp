namespace Shop
{
    public abstract class Employee
    {
        public string Name { get; protected set; }

        protected string _id;

        protected Employee(string name)
        {
            Name = name.Trim();
        }
    }

    public class Manager : Employee
    {
        private readonly int _grade;

        public Manager(string name, int grade, string id)
            : base(name)
        {
            _grade = grade;
            _id = id;
        }

        public int Grade => _grade;
    }
}
