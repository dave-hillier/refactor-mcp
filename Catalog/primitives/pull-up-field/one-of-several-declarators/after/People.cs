namespace Shop
{
    public class Employee
    {
        protected int _reports;
    }

    public class Manager : Employee
    {
        protected int _grade;

        public int Load() => _grade * _reports;
    }
}
