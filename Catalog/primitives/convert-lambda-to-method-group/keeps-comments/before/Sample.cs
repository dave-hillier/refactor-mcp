using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> Describe(int[] numbers)
    {
        return numbers
            .Select(
                // each as text
                /*^*/n => Format(n)) // formatted
            .ToList();
    }

    private static string Format(int number) => "#" + number;
}
