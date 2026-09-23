using System;

public class Sample
{
    public void Accept(int amount)
    {
        if (amount < 0)
        {
            /*[*/Console.WriteLine("Rejected");
            Console.WriteLine(amount);
            return;/*]*/
        }

        Console.WriteLine("Accepted");
    }
}
