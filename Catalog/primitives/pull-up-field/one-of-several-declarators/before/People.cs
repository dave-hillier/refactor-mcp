namespace Shop
{
    public class Employee
    {
    }

    public class Manager : Employee
    {
        protected int _grade, _reports;

        public int Load() => _grade * _reports;
    }
}
