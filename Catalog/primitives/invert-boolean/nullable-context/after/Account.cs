namespace Shop
{
    public class Account
    {
        public string? Owner { get; set; }

        public bool IsSuspended { get; set; } = false;

        public string Describe() => !IsSuspended && Owner is not null ? Owner : "inactive";
    }
}
