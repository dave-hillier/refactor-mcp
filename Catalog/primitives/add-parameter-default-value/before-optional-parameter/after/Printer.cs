namespace Shop;

public class Printer
{
    public string Print(string text, int copies = 1, bool colour = false)
    {
        return text + copies + colour;
    }

    public string Run()
    {
        return Print("a", colour: true) + Print("b") + Print("c", 2);
    }
}
