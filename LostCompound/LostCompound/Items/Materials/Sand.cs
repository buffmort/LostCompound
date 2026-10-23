using LostCompound.Items;

namespace LostCompound.Items.Materials
{
    public sealed class Sand(int count) : Item(ItemIds.Sand, "沙子", 99, true, count)
    {
        public override ItemType? Type => ItemType.Sand;
    }
}
