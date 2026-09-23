namespace Shop
{
    public class Validator
    {
        private readonly int _limit = 10;

        public bool IsValid(int quantity)
        {
            if (quantity < 0)
            {
                return false;
            }

            return quantity <= _limit;
        }

        public bool IsValid(string code) => code.Length > 0;
    }
}
