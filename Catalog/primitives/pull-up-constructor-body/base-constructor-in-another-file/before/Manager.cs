namespace Shop
{
    public class Manager : Employee
    {
        private readonly int _grade;

        public Manager(string name, string id, int grade)
        {
            _name = name;
            _id = id;
            _grade = grade;
        }

        public int Grade => _grade;
    }
}
