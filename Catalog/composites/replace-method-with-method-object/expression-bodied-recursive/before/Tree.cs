using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public class Node
    {
        public List<Node> Children { get; } = new List<Node>();
    }

    public class Tree
    {
        private readonly int _rootDepth = 1;

        private static int Deeper(int depth) => depth + 1;

        public int Depth(Node node) => node.Children.Count == 0 ? _rootDepth : Deeper(node.Children.Max(child => Depth(child)));
    }
}
