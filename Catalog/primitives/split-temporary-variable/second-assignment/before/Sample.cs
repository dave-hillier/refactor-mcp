using System;

public class Sample
{
    public void Print(double height, double width)
    {
        double temp = 2 * (height + width);
        Console.WriteLine(temp);
        /*^*/temp = height * width;
        Console.WriteLine(temp);
    }
}
