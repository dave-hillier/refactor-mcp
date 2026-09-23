using System;

public class Sample
{
    public void PrintWhole(double[] amounts)
    {
        for (int i = 0; i < amounts.Length; i++)
        {
            int whole = (int)amounts[i];
            Console.WriteLine(whole);
        }
    }
}
