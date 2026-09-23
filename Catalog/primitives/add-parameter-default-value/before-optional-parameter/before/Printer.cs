namespace Shop;

public class Printer
{
    public string Print(string text, int copies, bool colour = false)
    {
        return text + copies + colour;
    }

    public string Run()
    {
        return Print("a", 1, true) + Print("b", 1) + Print("c", 2);
    }
}
