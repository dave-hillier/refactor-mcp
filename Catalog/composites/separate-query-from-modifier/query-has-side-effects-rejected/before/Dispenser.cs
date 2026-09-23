namespace Tickets
{
    public class Dispenser
    {
        private int _issued;
        private int _next;

        public int Issue()
        {
            _issued++;
            return Take();
        }

        private int Take()
        {
            return _next++;
        }
    }
}
