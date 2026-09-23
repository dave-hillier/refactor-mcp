namespace Pets;

public abstract class Pet
{
    public string? Nickname { get; set; }

    public abstract string? Label();
}

public sealed class Dog : Pet
{
    public string Breed { get; set; } = "";

    public override string? Label() => Nickname ?? Breed;
}

public sealed class Cat : Pet
{
    public bool Indoor { get; set; }

    public override string? Label() => Indoor ? Nickname : null;
}

public class Vet
{
    public string? Label(Pet pet)
    {
        return pet.Label();
    }
}
