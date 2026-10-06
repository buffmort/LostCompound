namespace LostCompound.Items
{
    public sealed class GlassJar(int count) : Item(7, "玻璃罐", 99, true, count)
    {
        public override ItemType? Type => ItemType.GlassJar;
    }
}
