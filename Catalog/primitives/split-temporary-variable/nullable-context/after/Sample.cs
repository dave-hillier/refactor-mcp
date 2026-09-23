using System;

public class Sample
{
    public void Show(int id)
    {
        string? label = Find(id);
        Console.WriteLine(label ?? "none");
        string? backup = Find(id + 1);
        Console.WriteLine(backup ?? "none");
    }

    private string? Find(int id) => id > 0 ? "found" : null;
}
