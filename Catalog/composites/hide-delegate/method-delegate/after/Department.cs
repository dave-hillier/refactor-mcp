namespace Staff
{
    public class Department
    {
        public Employee Manager { get; set; } = new Employee();

        public string Describe(string prefix) => prefix + Manager.Name;
    }

    public class Employee
    {
        public string Name { get; set; } = "";

        public void Approve(Invoice invoice) => invoice.Approved = true;
    }

    public class Invoice
    {
        public bool Approved { get; set; }
    }
}
