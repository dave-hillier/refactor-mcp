using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> Describe(int[] numbers, int number)
    {
        return numbers.Where(n => n != number).Select(/*^*/Format).ToList();
    }

    private static string Format(int number) => "#" + number;
}
