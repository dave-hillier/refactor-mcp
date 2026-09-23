using System;
using System.Linq;

namespace Shop
{
    public class Customer : ICustomer
    {
        private readonly string[] _tags = new string[3];

        public string Name { get; set; }

        public event EventHandler Renamed;

        public string this[int index] => _tags[index];

        public void Rename(string name)
        {
            Name = name;
            Renamed?.Invoke(this, EventArgs.Empty);
        }

        public int TagCount() => _tags.Count(t => t != null);

        public static Customer Create() => new Customer();

        private void Reset() => Name = null;
    }
}
