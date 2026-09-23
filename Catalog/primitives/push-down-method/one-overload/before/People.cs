namespace Shop
{
    public class Employee
    {
        public string Report() => "all";

        public string Report(int year) => "year " + year;
    }

    public class Salesman : Employee
    {
        public string Annual() => Report(2024) + Report();
    }
}
