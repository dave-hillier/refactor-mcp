public class Sample
{
    public string Describe(int value)
    {
        /*^*/if (value > 0)
        {
            return "positive";
        }
        else
        {
            return "not positive";
        }
    }
}
