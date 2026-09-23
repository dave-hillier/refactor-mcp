using System;

public class Sample
{
    public void Accept(int amount)
    {
        if (amount < 0)
        {
            LogRejection(amount);
            return;
        }

        Console.WriteLine("Accepted");
    }

    private void LogRejection(int amount)
    {
        Console.WriteLine("Rejected");
        Console.WriteLine(amount);
    }
}
