#nullable enable
using System.Collections.Generic;

namespace Shop
{
    public class Catalogue
    {
        private readonly Dictionary<string, Product> _products = new Dictionary<string, Product>();

        public Product? Find(string sku) => _products.TryGetValue(sku, out var product) ? product : null;
    }
}
