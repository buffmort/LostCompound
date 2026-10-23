using LostCompound.Items;

namespace LostCompound.Items.Containers
{
    public sealed class GlassJar(int count) : Item(ItemIds.GlassJar, "玻璃罐", 10, true, count)
    {
        public override ItemType? Type => ItemType.GlassJar;
    }
}
