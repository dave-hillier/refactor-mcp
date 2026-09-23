using System;

namespace Shop;

public class Rounding
{
    public double Round(double value)
    {
        return Math.Round(value, 2);
    }

    public decimal Round(decimal value, int decimals)
    {
        return decimal.Round(value, decimals);
    }

    public string Show()
    {
        return Round(1.234m, 2) + " " + Round(1.234);
    }
}
