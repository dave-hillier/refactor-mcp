namespace Shop;

public class Browser
{
    public string Show(Catalogue catalogue)
    {
        return catalogue.Page(1) + catalogue.Page(2, 50) + catalogue.Page(3);
    }
}
