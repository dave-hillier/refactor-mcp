public class Sample
{
    public string Service(int weight, bool express)
    {
        if (/*[*/weight < 1000 && !express/*]*/)
            return "standard";
        return "courier";
    }

    public int Limit()
    {
        return 1000;
    }
}
