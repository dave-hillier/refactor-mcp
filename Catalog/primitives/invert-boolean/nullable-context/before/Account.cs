namespace Shop
{
    public class Account
    {
        public string? Owner { get; set; }

        public bool IsActive { get; set; } = true;

        public string Describe() => IsActive && Owner is not null ? Owner : "inactive";
    }
}
