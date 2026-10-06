using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public class Wood : Item, TurnCount, Changeable
    {
        public int change = 3;

        public Wood(int count)
        {
            this.name = "木头";
            this.id = 2;
            this.maxStack = 99;
            this.Stackable = true;
            this.count = count;
            this.type = ItemType.WOOD;
        }

        public override ItemType? getType()
        {
            return ItemType.WOOD;
        }


        public void onBattleStart(Player p, EnvType envType)
        {
            if (envType == EnvType.LAVA_WATERFALL)
            {
                change -= 1;
                Console.WriteLine("木头:" + change + "回合之后...");
            }
        }

        public void onBattleEnd(Player p, EnvType envType)
        {

        }

        public bool ShouldRemove()
        {
            return this.change <= 0;
        }

        public Item Become()
        {
            Console.Write("木头变成了碳!");
            return new Charcoal(count);
        }
    }
}
