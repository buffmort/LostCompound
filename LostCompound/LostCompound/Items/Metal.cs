using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>金属：材料。消耗 1 个可永久 +1 DEF。</summary>
    /// <remarks>
    /// 同时实现 Changeable：数量归零后由 Player.HandleChangeableItems 自动移出背包，
    /// 否则会留下一个「金属 x0」。这是本项目中消耗品的统一做法（参见 WhitePhosphorus）。
    /// </remarks>
    public sealed class Metal(int count) : Item(ItemIds.Metal, "金属", 99, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.Metal;

        public void Use(Player p, List<Monster> m)
        {
            // 改的是**基础值**，不是派生的 p.Def —— 改造前写的是 `p.Def += 1`，
            // 而每回合开头的护盾重置会顺带把 Def 还原成基础值，于是这 1 点 DEF 在下一回合
            // 开头就没了（不报错、不崩溃，只是静默失效）。走 GrowBaseDef 才是真的永久 +1。
            p.GrowBaseDef(1);
            Count -= 1;
            Console.WriteLine("你消耗了一块金属，DEF +1（当前 DEF: " + p.Def + "）");
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("金属只能在战斗中使用！");
        }

        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}
