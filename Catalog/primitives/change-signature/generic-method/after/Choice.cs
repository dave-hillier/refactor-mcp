namespace Shop;

public static class Choice
{
    public static T Pick<T>(T first, T second, bool preferFirst)
    {
        return preferFirst ? first : second;
    }
}

public class Menu
{
    public string Main()
    {
        return Choice.Pick("soup", "salad", true) + Choice.Pick<int>(1, 2, false);
    }
}
