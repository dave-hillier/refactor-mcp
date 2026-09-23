namespace Shop
{
    public class Directory
    {
        private string _owner;

        public void Claim(string owner)
        {
            _owner = owner;
        }

        public int OwnerLength() => _owner.Length;
    }
}
