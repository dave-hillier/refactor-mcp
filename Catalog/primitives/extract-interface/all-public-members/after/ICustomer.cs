using System;

namespace Shop
{
    public interface ICustomer
    {
        string Name { get; set; }
        event EventHandler Renamed;
        string this[int index] { get; }
        void Rename(string name);
        int TagCount();
    }
}
