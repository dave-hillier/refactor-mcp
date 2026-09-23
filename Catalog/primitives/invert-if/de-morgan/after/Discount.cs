namespace Shop
{
    public class Discount
    {
        public decimal Rate(int quantity, bool member, string code)
        {
            if ((quantity < 10 || member) && code != null)
            {
                return 0m;
            }
            else
            {
                return 0.1m;
            }
        }
    }
}
