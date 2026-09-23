namespace Shop;

public class Catalogue
{
    public string Page(int number, int size)
    {
        return number + "/" + size;
    }
}
