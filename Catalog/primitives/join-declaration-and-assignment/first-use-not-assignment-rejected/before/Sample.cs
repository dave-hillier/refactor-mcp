public class Sample
{
    public int Pick(bool first)
    {
        int /*^*/choice;
        if (first)
        {
            choice = 1;
        }
        else
        {
            choice = 2;
        }

        return choice;
    }
}
