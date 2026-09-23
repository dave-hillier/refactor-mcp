namespace Shop
{
    public class Hall
    {
        private int _free = 10;

        public void Reserve(int seats)
        {
            if (seats > _free)
                throw new SeatsUnavailableException();

            _free -= seats;
        }

        public string Book(int seats)
        {
            string answer;
            try
            {
                Reserve(seats);
                answer = "Booked";
            }
            catch (SeatsUnavailableException)
            {
                answer = "Full";
            }

            return answer;
        }
    }
}
