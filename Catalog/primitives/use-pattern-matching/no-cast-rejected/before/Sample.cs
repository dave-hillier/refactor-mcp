namespace Shop
{
    public class Sample
    {
        public bool IsText(object value)
        {
            /*^*/if (value is string)
            {
                return true;
            }

            return false;
        }
    }
}
