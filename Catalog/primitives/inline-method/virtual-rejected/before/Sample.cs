public class Animal
{
    public virtual string Sound() => "...";

    public string Speak() => Sound() + "!";
}

public class Dog : Animal
{
    public override string Sound() => "Woof";
}
