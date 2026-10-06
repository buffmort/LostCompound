namespace LostCompound.Items
{
    public class Sand : Item
    {
        public Sand(int count)
        {
            this.name = "沙子";
            this.id = 6;
            this.count = count;
            this.Stackable = true;
            this.maxStack = 99;
            this.type = ItemType.SAND;
        }

        public override ItemType? getType()
        {
            return this.type;
        }
    }
}
