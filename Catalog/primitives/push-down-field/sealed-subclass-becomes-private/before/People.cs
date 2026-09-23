namespace Shop
{
    public class Employee
    {
        protected int Quota;
    }

    public sealed class Salesman : Employee
    {
        public void Assign(int quota) => Quota = quota;

        public bool Met(int sales) => sales >= Quota;
    }
}
