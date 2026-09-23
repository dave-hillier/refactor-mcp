using System;

public class Sample
{
    public void Report(int[] values, int target)
    {
        var found = false;
        /*^*/foreach (var value in values)
        {
            if (value == target)
            {
                found = true;
                break;
            }
        }

        Console.WriteLine(found);
    }
}
