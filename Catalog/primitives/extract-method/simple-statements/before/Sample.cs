using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        /*[*/if (a < 0 || b < 0)
        {
            throw new ArgumentException();
        }/*]*/
        var result = a + b;
        return result;
    }
}
