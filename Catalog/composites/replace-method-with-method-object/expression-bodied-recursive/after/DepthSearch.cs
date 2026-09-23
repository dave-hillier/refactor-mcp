using System.Linq;

namespace Shop
{
    public class DepthSearch
    {
        private readonly Tree _tree;
        private readonly Node _node;

        public DepthSearch(Tree tree, Node node)
        {
            _tree = tree;
            _node = node;
        }

        public int Compute() => _node.Children.Count == 0 ? _tree._rootDepth : Tree.Deeper(_node.Children.Max(child => _tree.Depth(child)));
    }
}
