using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        int limit = 10;
        /*[*/if (a > limit)
        {
            return a + b;
        }/*]*/
        return 0;
    }
}
