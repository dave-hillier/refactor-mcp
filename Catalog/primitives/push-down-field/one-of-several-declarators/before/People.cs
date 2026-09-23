namespace Shop
{
    public class Employee
    {
        protected int Grade, Quota;

        public int Level() => Grade;
    }

    public class Salesman : Employee
    {
        public int Target() => Quota * Grade;
    }
}
