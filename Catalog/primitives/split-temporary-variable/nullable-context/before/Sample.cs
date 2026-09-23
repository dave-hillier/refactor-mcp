using System;

public class Sample
{
    public void Show(int id)
    {
        string? label = Find(id);
        Console.WriteLine(label ?? "none");
        /*^*/label = Find(id + 1);
        Console.WriteLine(label ?? "none");
    }

    private string? Find(int id) => id > 0 ? "found" : null;
}
