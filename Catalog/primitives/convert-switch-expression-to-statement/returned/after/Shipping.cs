namespace Shop
{
    public class Shipping
    {
        public string Zone(int code)
        {
            switch (code)
            {
                case 1:
                    return "local";
                case 2:
                case 3:
                    return "national";
                default:
                    return "international";
            }
        }
    }
}
