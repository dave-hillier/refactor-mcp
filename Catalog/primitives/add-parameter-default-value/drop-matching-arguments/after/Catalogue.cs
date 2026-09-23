namespace Shop;

public class Catalogue
{
    public string Page(int number, int size = 20)
    {
        return number + "/" + size;
    }
}
