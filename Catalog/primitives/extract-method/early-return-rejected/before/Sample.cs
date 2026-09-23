using System;

public class Sample
{
    public void Accept(int amount)
    {
        /*[*/if (amount < 0)
            return;
        Console.WriteLine("Checked");/*]*/
        Console.WriteLine("Accepted");
    }
}
