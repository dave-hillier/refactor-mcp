using System;

namespace Shop;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }

    public Func<int, int, int> Operation()
    {
        return Add;
    }
}
