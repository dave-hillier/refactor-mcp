using System;

public class Sample
{
    public void PrintWhole(double[] amounts)
    {
        /*^*/foreach (int whole in amounts)
        {
            Console.WriteLine(whole);
        }
    }
}
