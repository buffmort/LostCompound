using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 红磷（设计文档 ID 31，配方 0）：消耗品。
    /// 烟雾 2 回合：全体怪物 DOT 10（困难 7）+ 50%（困难 40%）概率加格挡。
    ///
    /// 注意：本类必须同时实现 IChangeable，否则 Count 减到 0 之后
    /// 会留下一个「红磷 x0」永远占着背包格（这是 Metal 上踩过的同一个坑）。
    /// </summary>
    public sealed class RedPhosphorus(int count) : Item(ItemIds.RedPhosphorus, "红磷", 99, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.RedPhosphorus;

        public void Use(Player p, List<Monster> m)
        {
            int dotDmg = 10;
            int dotdur = 2;
            double chance = 0.5;
            Console.WriteLine("你点燃了红磷!");
            Console.WriteLine("烟雾弥漫了整个房间...");
            foreach (Monster monsters in m)
            {
                monsters.AddDotDuration(dotdur);
                monsters.AddDotDamage(dotDmg);
            }
            Console.WriteLine("全部怪物受到" + dotdur + "时长的 " + dotDmg + "伤害!");
            if (Random.Shared.NextDouble() < chance)
            {
                p.Block += 1;
                Console.WriteLine("烟雾掩护了你！你获得了一点格挡");
            }
            else
            {
                Console.WriteLine("红磷的烟雾并没有掩护你!");
            }

            Count -= 1;
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("你不能在非战斗下使用红磷！");
        }

        /// <summary>用光即移出背包。</summary>
        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}
