using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>金属：材料。消耗 1 个可永久 +1 DEF。</summary>
    public sealed class Metal(int count) : Item(ItemIds.Metal, "金属", 99, true, count), Useable
    {
        public override ItemType? Type => ItemType.Metal;

        public void Use(Player p, List<Monster> m)
        {
            p.Def += 1;
            Count -= 1;
            Console.WriteLine("你消耗了一块金属，DEF +1（当前 DEF: " + p.Def + "）");
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("金属只能在战斗中使用！");
        }
    }
}
