namespace Shop
{
    public class Discount
    {
        public decimal Rate(bool member, bool student, int quantity)
        {
            /*^*/if (member || student)
            {
                if (quantity > 10 || quantity < 0)
                {
                    return 0.1m;
                }
            }

            return 0m;
        }
    }
}
