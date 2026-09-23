namespace Shop
{
    public class Bulk : Pricing
    {
        public int Four(int amount) => Twice(Twice(amount));
    }
}
