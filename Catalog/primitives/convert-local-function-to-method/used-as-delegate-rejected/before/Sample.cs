using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<int> WithTax(int[] prices, int rate)
    {
        return prices.Select(Apply).ToList();

        int /*^*/Apply(int price) => price * (100 + rate) / 100;
    }
}
