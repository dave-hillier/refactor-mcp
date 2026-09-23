namespace Shop
{
    public class Sign
    {
        public int Of(bool negative)
        {
            if (negative)
                return -1;
            else
                return 1;
        }
    }
}
