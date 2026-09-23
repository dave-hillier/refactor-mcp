namespace Shop
{
    public class Labels
    {
        public string For(Customer customer)
        {
            customer.Address.Lines.Add("1 High Street");
            return customer.Address.Format();
        }
    }
}
