namespace Shop
{
    public class Shipping
    {
        public string Zone(int code)
        {
            return code /*^*/switch
            {
                1 => "local",
                2 or 3 => "national",
                _ => "international",
            };
        }
    }
}
