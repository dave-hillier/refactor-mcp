using System.Collections;

public class Sample
{
    public int CountNonNull(ArrayList items)
    {
        var count = 0;
        /*^*/foreach (object item in items)
        {
            if (item != null)
                count++;
        }

        return count;
    }
}
