namespace Venue
{
    public class Tickets
    {
        private decimal _price = 10m;

        public decimal Fee(int age, bool member)
        {
            if (IsExempt(age, member))
            {
                return 0m;
            }

            return _price;
        }

        private bool IsExempt(int age, bool member)
        {
            return age >= 65 || member;
        }

        public decimal Deposit() => _price / 2;
    }
}
