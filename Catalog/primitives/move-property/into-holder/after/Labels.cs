namespace Shop
{
    public class Labels
    {
        public string For(Customer customer) => customer.Full.ToUpper();
    }
}
