using Core;

namespace App
{
    public class Program
    {
        public string Run(Catalog catalog) => catalog.Find("A1");
    }
}
