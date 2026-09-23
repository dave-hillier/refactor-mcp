namespace Shop
{
    public class Sample
    {
        public int Length(object value, object other)
        {
            /*^*/if (value is string)
            {
                return ((string)value).Length;
            }

            return 0;
        }
    }
}
