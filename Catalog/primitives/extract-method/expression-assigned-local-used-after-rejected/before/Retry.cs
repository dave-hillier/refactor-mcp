namespace Net
{
    public class Retry
    {
        public bool Next(int attempts)
        {
            if (/*[*/++attempts > 3/*]*/)
            {
                return false;
            }

            return attempts % 2 == 0;
        }
    }
}
