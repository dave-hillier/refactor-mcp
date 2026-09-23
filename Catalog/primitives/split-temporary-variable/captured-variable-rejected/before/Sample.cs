using System;

public class Sample
{
    public void Print(int a)
    {
        int value = a;
        Action print = () => Console.WriteLine(value);
        print();
        /*^*/value = 0;
        print();
    }
}
