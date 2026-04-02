using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct ResourceNodeData
{
    public int NodeTypeId;
    public int ResourceItemTypeId;
    public int MinDrop;
    public int MaxDrop;
}
