namespace Shop
{
    public class Sign
    {
        public string Describe(bool positive)
        {
            return positive /*^*/switch
            {
                true => "positive",
                false => "not positive",
            };
        }
    }
}
