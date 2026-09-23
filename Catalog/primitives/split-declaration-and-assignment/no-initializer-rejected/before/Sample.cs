public class Sample
{
    public int Pick(bool first)
    {
        int /*^*/choice;
        choice = first ? 1 : 2;
        return choice;
    }
}
