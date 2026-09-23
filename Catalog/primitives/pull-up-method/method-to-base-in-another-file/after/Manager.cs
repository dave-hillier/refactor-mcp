namespace Shop
{
    public class Manager : Employee
    {
        public int Grade;

        public bool IsSenior() => Grade > 2;
    }
}
