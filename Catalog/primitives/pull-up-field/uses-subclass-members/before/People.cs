namespace Shop
{
    public class Employee
    {
    }

    public class Manager : Employee
    {
        private const int Grade = 3;
        private int _bonus = Grade * 100;

        public int Bonus() => _bonus;
    }
}
