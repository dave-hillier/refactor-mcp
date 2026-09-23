namespace Shop
{
    public class Hall
    {
        private int _free = 10;

        public int Reserve(int seats)
        {
            if (seats > _free)
                return -1;

            _free -= seats;
            return 0;
        }

        public string Book(int seats)
        {
            string answer;
            if (Reserve(seats) == 0)
            {
                answer = "Booked";
            }
            else
            {
                answer = "Full";
            }

            return answer;
        }
    }
}
