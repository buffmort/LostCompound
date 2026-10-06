namespace LostCompound.Items
{
    public class Sulphur : Item
    {
        public Sulphur(int count)
        {
            this.name = "硫磺";
            this.id = 1;
            this.maxStack = 999;
            this.Stackable = true;
            this.count = count;
        }

        public override ItemType? getType()
        {
            return ItemType.SULPHUR;
        }
    }
}
