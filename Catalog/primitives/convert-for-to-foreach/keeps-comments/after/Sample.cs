using System;

public class Sample
{
    public void Print(string[] names)
    {
        Console.WriteLine("Names:");

        // One line per name.
        foreach (string name in names) // every name
        {
            // Indented under the heading.
            Console.WriteLine("  " + name);
        }
    }
}
