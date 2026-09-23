namespace Shop;

public readonly struct Temperature
{
    public Temperature(double celsius)
    {
        Celsius = celsius;
    }

    public double Celsius { get; }

    public double Fahrenheit => Celsius * 9 / 5 + 32;
}
