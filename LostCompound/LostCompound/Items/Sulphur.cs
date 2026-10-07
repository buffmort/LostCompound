namespace LostCompound.Items
{
    public sealed class Sulphur(int count) : Item(ItemIds.Sulphur, "硫磺", 999, true, count)
    {
        public override ItemType? Type => ItemType.Sulphur;
    }
}
