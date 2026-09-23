using System;

namespace Shop;

public class Rounding
{
    public double Round(double value)
    {
        return Math.Round(value, 2);
    }

    public decimal Round(decimal value)
    {
        return decimal.Round(value, /*[*/2/*]*/);
    }

    public string Show()
    {
        return Round(1.234m) + " " + Round(1.234);
    }
}
