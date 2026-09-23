namespace Shop;

public class Bank
{
    public Account Open(string name)
    {
        Account first = new Account(name);
        Account second = new("joint");
        return first.Owner == name ? first : second;
    }
}
