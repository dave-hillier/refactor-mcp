namespace Shop;

public class Directory
{
    public string Find(string name)
    {
        return name.Length > 0 ? name : "";
    }

    public int Length(string name)
    {
        return Find(name).Length;
    }
}
