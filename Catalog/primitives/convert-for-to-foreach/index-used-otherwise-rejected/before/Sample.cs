using System;

public class Sample
{
    public void Print(string[] names)
    {
        /*^*/for (int i = 0; i < names.Length; i++)
        {
            Console.WriteLine(i + ": " + names[i]);
        }
    }
}
