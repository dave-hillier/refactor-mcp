using System;

namespace Shop;

public static class Features
{
    public static bool IsEnabled(string flag) => flag == "Sale";
}

public static class Banner
{
    public static void Show()
    {
        Sale.Apply();
    }

    private static ISaleStrategy Sale => Features.IsEnabled("Sale") ? new SaleStrategy() : new NoSaleStrategy();
}

internal interface ISaleStrategy
{
    void Apply();
}

internal sealed class SaleStrategy : ISaleStrategy
{
    public void Apply()
    {
        Console.WriteLine("Sale on now");
    }
}

internal sealed class NoSaleStrategy : ISaleStrategy
{
    public void Apply()
    {
        Console.WriteLine("Welcome");
    }
}
