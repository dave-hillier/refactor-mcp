namespace Shop
{
    public class Deal
    {
        public decimal Total { get; private set; }

        public void Price(decimal price, bool special)
        {
            /*^*/if (special)
            {
                Total = price * 0.95m;
                Send();
            }
            else
            {
                Total = price * 0.98m;
                Send();
            }
        }

        private void Send()
        {
        }
    }
}
