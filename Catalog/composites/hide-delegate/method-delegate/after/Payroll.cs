namespace Staff
{
    public class Payroll
    {
        public void Submit(Person person, Invoice invoice)
        {
            person.Department.Manager.Approve(invoice);
        }

        public string Heading(Person person) => person.DescribeDepartment("Department of ") + ": " + person.Name;
    }
}
