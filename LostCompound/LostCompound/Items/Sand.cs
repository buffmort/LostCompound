namespace LostCompound.Items
{
    public sealed class Sand(int count) : Item(6, "沙子", 99, true, count)
    {
        public override ItemType? Type => ItemType.Sand;
    }
}
