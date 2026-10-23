using LostCompound.Items;

namespace LostCompound.Items.Capabilities
{
    public interface IChangeable
    {
        bool ShouldRemove();
        Item Become();
    }
}
