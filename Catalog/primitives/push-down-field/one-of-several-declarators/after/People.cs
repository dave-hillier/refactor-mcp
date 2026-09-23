namespace Shop
{
    public class Employee
    {
        protected int Grade;

        public int Level() => Grade;
    }

    public class Salesman : Employee
    {
        protected int Quota;

        public int Target() => Quota * Grade;
    }
}
