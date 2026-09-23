using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> Describe(int[] numbers)
    {
        return numbers.Select(Format).ToList();

        static string Format(int number) => "#" + number;
    }
}
