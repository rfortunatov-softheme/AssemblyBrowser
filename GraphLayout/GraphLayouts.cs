using AssemblyBrowser;
using GraphSharp.Controls;
using QuickGraph;

namespace GraphLayout
{
    public class DependencyGraph : BidirectionalGraph<TypeInfo, IEdge<TypeInfo>> { }

    public class DependencyGraphLayout : GraphLayout<TypeInfo, IEdge<TypeInfo>, DependencyGraph> { }
}
