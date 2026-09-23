namespace Shop
{
    public class Ticket
    {
        private int _counter;

        public int Pair()
        {
            int /*^*/next = NextNumber();
            return next + next;
        }

        private int NextNumber()
        {
            return _counter++;
        }
    }
}
