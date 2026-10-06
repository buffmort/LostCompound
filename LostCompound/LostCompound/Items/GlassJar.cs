namespace LostCompound.Items
{
    public class GlassJar : Item
    {
        public GlassJar(int count)
        {
            this.name = "玻璃罐";
            this.id = 7;
            this.count = count;
            this.maxStack = 99;
            this.Stackable = true;
        }

        public override ItemType? getType()
        {
            return ItemType.GLASS_JAR;
        }
    }
}
