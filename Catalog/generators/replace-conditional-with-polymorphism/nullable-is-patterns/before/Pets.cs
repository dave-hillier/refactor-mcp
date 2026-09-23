using System;

namespace Pets;

public abstract class Pet
{
    public string? Nickname { get; set; }
}

public sealed class Dog : Pet
{
    public string Breed { get; set; } = "";
}

public sealed class Cat : Pet
{
    public bool Indoor { get; set; }
}

public class Vet
{
    public string? Label(Pet pet)
    {
        if (pet is Dog dog)
            return dog.Nickname ?? dog.Breed;
        else if (pet is Cat cat)
            return cat.Indoor ? cat.Nickname : null;
        else
            throw new NotSupportedException();
    }
}
