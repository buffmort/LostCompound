namespace LostCompound.Items
{
    public sealed class GlassJar(int count) : Item(ItemIds.GlassJar, "玻璃罐", 99, true, count)
    {
        public override ItemType? Type => ItemType.GlassJar;
    }
}
