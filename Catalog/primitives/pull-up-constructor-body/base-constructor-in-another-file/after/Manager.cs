namespace Shop
{
    public class Manager : Employee
    {
        private readonly int _grade;

        public Manager(string name, string id, int grade)
            : base(name, id)
        {
            _grade = grade;
        }

        public int Grade => _grade;
    }
}
