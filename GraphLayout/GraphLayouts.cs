using AssemblyBrowser;
using GraphSharp.Controls;
using QuickGraph;

namespace GraphLayout
{
    public class DependencyGraph : BidirectionalGraph<TypeInfo, IEdge<TypeInfo>>
    {
        public override bool AddEdge(IEdge<TypeInfo> e)
        {
            EdgeCapacity++;
            return base.AddEdge(e);
        }
    }

    public class DependencyGraphLayout : GraphLayout<TypeInfo, IEdge<TypeInfo>, DependencyGraph> { }
}
