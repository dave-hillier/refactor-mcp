namespace Shop;

public class Printer
{
    public string Print(object value)
    {
        return "object";
    }

    public string Print(string value)
    {
        return "string";
    }
}

public class Label
{
    public string Show(Printer printer)
    {
        return printer.Print("sale");
    }
}
