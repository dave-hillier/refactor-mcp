public class Sample
{
    public string Service(int weight, bool express)
    {
        if (IsStandardParcel(weight, express))
            return "standard";
        return "courier";
    }

    private bool IsStandardParcel(int weight, bool express)
    {
        return weight < 1000 && !express;
    }

    public int Limit()
    {
        return 1000;
    }
}
