namespace Staff
{
    public class Payroll
    {
        public void Submit(Person person, Invoice invoice)
        {
            person.ApproveByManager(invoice);
        }
    }
}
