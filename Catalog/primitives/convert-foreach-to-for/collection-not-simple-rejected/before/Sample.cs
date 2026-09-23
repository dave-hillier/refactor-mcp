using System;

public class Sample
{
    private int[] Values() => new[] { 1, 2, 3 };

    public void Print()
    {
        /*^*/foreach (var value in Values())
        {
            Console.WriteLine(value);
        }
    }
}
