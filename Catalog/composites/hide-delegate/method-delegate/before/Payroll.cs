namespace Staff
{
    public class Payroll
    {
        public void Submit(Person person, Invoice invoice)
        {
            person.Department.Manager.Approve(invoice);
        }

        public string Heading(Person person) => person.Department.Describe("Department of ") + ": " + person.Name;
    }
}
