using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public sealed class Wood(int count) : Item(2, "木头", 99, true, count), TurnCount, Changeable
    {
        public int Change { get; set; } = 3;

        public override ItemType? Type => ItemType.Wood;

        public void OnBattleStart(Player p, EnvType envType)
        {
            if (envType == EnvType.LavaWaterfall)
            {
                Change -= 1;
                Console.WriteLine("木头:" + Change + "回合之后...");
            }
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {

        }

        public bool ShouldRemove()
        {
            return this.Change <= 0;
        }

        public Item Become()
        {
            Console.Write("木头变成了碳!");
            return new Charcoal(Count);
        }
    }
}
