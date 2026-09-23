namespace Accounts
{
    public class Ledger
    {
        private int _balance;

        public void Drain(int step)
        {
            /*^*/while (_balance > 0)
            {
                _balance -= step;
            }
        }
    }
}
