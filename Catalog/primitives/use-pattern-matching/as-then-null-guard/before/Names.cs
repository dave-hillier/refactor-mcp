namespace Shop
{
    public class Names
    {
        public int Length(object value)
        {
            string? name = value as string;
            /*^*/if (name is null)
            {
                return 0;
            }

            return name.Length;
        }
    }
}
