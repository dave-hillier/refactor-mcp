namespace Shop;

public static class Choice
{
    public static T Pick<T>(bool preferFirst, T first, T second)
    {
        return preferFirst ? first : second;
    }
}

public class Menu
{
    public string Main()
    {
        return Choice.Pick(true, "soup", "salad") + Choice.Pick<int>(false, 1, 2);
    }
}
