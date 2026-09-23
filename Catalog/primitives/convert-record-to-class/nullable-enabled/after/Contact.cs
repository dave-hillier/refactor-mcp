using System;
using System.Collections.Generic;

namespace Shop;

public sealed class Contact : IEquatable<Contact>
{
    public Contact(string name, string? email)
    {
        Name = name;
        Email = email;
    }

    public string Name { get; init; }

    public string? Email { get; init; }

    public void Deconstruct(out string name, out string? email)
    {
        name = Name;
        email = Email;
    }

    public bool Equals(Contact? other)
    {
        return other is not null
            && EqualityComparer<string>.Default.Equals(Name, other.Name)
            && EqualityComparer<string?>.Default.Equals(Email, other.Email);
    }

    public override bool Equals(object? obj) => Equals(obj as Contact);

    public override int GetHashCode() => HashCode.Combine(Name, Email);

    public override string ToString() => $"Contact {{ Name = {Name}, Email = {Email} }}";

    public static bool operator ==(Contact? left, Contact? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Contact? left, Contact? right) => !(left == right);
}
