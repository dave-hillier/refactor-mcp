namespace Shop;

public class Bank
{
    public Account Open(string name)
    {
        Account first = new Account(name, 0m);
        Account second = new("joint", 0m);
        return first.Owner == name ? first : second;
    }
}
