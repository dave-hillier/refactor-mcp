namespace Shop
{
    public class Sample
    {
        public string Name(int code)
        {
            return "code " + (code /*^*/switch
            {
                1 => "one",
                _ => "other",
            });
        }
    }
}
