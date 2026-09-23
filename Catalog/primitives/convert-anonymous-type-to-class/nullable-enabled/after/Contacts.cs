using System;
using System.Collections.Generic;

namespace Shop;

public class Contacts
{
    public string Describe(string name, string? email)
    {
        var contact = new Contact(name, email);
        return contact.Name + (contact.Email ?? "");
    }
}

internal sealed class Contact
{
    public Contact(string name, string? email)
    {
        Name = name;
        Email = email;
    }

    public string Name { get; }

    public string? Email { get; }

    public override bool Equals(object? obj)
    {
        return obj is Contact other
            && EqualityComparer<string>.Default.Equals(Name, other.Name)
            && EqualityComparer<string?>.Default.Equals(Email, other.Email);
    }

    public override int GetHashCode() => HashCode.Combine(Name, Email);

    public override string ToString() => $"{{ Name = {Name}, Email = {Email} }}";
}
