namespace Shop
{
    public class Finder
    {
        public T Find<T>(object value) where T : class
        {
            /*^*/if (value is T)
            {
                return (T)value;
            }

            return null;
        }
    }
}
