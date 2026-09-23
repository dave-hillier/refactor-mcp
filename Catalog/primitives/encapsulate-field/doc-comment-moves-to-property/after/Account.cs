namespace Shop
{
    public class Account
    {
        // Kept in pence.
        private long _balance;

        public string Owner = "";

        /// <summary>The current balance.</summary>
        public long Balance
        {
            get => _balance;
            set => _balance = value;
        }
    }
}
