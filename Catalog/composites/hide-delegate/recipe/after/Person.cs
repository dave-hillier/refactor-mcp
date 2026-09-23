namespace Staff
{
    public class Person
    {
        public string Name { get; set; } = "";

        public Department Department { get; set; } = new Department();

        internal void ApproveByManager(Invoice invoice)
        {
            Department.Manager.Approve(invoice);
        }
    }
}
