namespace Shop;

public class Shelf
{
    public string Labels(Catalogue catalogue)
    {
        var labels = "";
        foreach (var name in catalogue.Names())
        {
            labels += name;
        }

        return labels;
    }
}
