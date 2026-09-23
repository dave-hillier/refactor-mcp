namespace Shop
{
    public class Customer
    {
        public string Name { get; set; } = "";
    }

    public class Admin
    {
        public void Rename(Customer customer, string name) => customer.Name = name;
    }
}
