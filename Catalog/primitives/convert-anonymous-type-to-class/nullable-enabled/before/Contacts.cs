using System;
using System.Collections.Generic;

namespace Shop;

public class Contacts
{
    public string Describe(string name, string? email)
    {
        var contact = /*^*/new { Name = name, Email = email };
        return contact.Name + (contact.Email ?? "");
    }
}
