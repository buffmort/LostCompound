using LostCompound.Items;

namespace LostCompound.Items.Materials
{
    public sealed class Sulphur(int count) : Item(ItemIds.Sulphur, "硫磺", 99, true, count)
    {
        public override ItemType? Type => ItemType.Sulphur;
    }
}
