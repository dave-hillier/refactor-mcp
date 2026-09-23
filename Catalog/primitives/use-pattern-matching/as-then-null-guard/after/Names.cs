namespace Shop
{
    public class Names
    {
        public int Length(object value)
        {
            if (value is not string name)
            {
                return 0;
            }

            return name.Length;
        }
    }
}
