namespace Shop;

public sealed class Contact
{
    public Contact(string name, string? email)
    {
        Name = name;
        Email = email;
    }

    public string Name { get; }

    public string? Email { get; }
}
