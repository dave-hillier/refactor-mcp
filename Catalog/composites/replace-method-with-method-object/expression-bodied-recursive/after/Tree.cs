using System.Collections.Generic;

namespace Shop
{
    public class Node
    {
        public List<Node> Children { get; } = new List<Node>();
    }

    public class Tree
    {
        internal readonly int _rootDepth = 1;

        internal static int Deeper(int depth) => depth + 1;

        public int Depth(Node node) => new DepthSearch(this, node).Compute();
    }
}
