using System;

public class Sample
{
    public void Print(string[] names)
    {
        Console.WriteLine("Names:");

        // One line per name.
        for (int i = 0; i < names.Length; i++) // every name
        {
            string name = names[i];
            // Indented under the heading.
            Console.WriteLine("  " + name);
        }
    }
}
