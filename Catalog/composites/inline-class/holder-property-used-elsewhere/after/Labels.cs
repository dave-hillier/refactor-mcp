namespace Shop
{
    public class Labels
    {
        public string For(Customer customer)
        {
            customer.Lines.Add("1 High Street");
            return customer.Format();
        }
    }
}
