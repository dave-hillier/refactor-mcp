namespace Venue
{
    public class Tickets
    {
        private decimal _price = 10m;

        public decimal Fee(int age, bool member)
        {
            if (/*[*/age >= 65 || member/*]*/)
            {
                return 0m;
            }

            return _price;
        }

        public decimal Deposit() => _price / 2;
    }
}
