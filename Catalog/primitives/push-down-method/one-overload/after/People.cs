namespace Shop
{
    public class Employee
    {
        public string Report() => "all";
    }

    public class Salesman : Employee
    {
        public string Annual() => Report(2024) + Report();

        public string Report(int year) => "year " + year;
    }
}
