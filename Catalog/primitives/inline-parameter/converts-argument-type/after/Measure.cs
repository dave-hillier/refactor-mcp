namespace Shop;

public class Measure
{
    public double Half(double value)
    {
        return value / (double)2;
    }

    public double Run()
    {
        return Half(9) + Half(5);
    }
}
