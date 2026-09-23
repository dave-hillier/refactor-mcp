namespace Shop
{
    public class Employee
    {
    }

    public sealed class Salesman : Employee
    {
        private int Quota;

        public void Assign(int quota) => Quota = quota;

        public bool Met(int sales) => sales >= Quota;
    }
}
