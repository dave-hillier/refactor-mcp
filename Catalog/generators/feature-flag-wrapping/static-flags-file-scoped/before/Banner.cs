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
        if (Features.IsEnabled("Sale"))
        {
            Console.WriteLine("Sale on now");
        }
        else
        {
            Console.WriteLine("Welcome");
        }
    }
}
