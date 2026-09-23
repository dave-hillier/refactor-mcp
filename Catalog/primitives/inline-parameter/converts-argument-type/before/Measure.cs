namespace Shop;

public class Measure
{
    public double Half(double value, double divisor)
    {
        return value / divisor;
    }

    public double Run()
    {
        return Half(9, 2) + Half(5, 2);
    }
}
