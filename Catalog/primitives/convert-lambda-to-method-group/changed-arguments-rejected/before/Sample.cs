using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> Describe(int[] numbers)
    {
        return numbers.Select(/*^*/n => Format(n + 1)).ToList();
    }

    private static string Format(int number) => "#" + number;
}
