using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Buffs;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Consumables
{
    /// <summary>
    /// 红磷（设计文档 ID 31，配方 0）：消耗品。
    /// 烟雾 2 回合：全体怪物持续伤害 10（困难 7）+ 50%（困难 40%）概率加护盾。
    /// 原先给全体挂的是 DOT 10 / 2 回合；DOT 机制已删除，现在挂的是等值的流血
    /// （每回合伤害 10、剩余 2 回合）。
    ///
    /// 注意：本类必须同时实现 IChangeable，否则 Count 减到 0 之后
    /// 会留下一个「红磷 x0」永远占着背包格（这是 Metal 上踩过的同一个坑）。
    /// </summary>
    public sealed class RedPhosphorus(int count) : Item(ItemIds.RedPhosphorus, "红磷", 99, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.RedPhosphorus;

        public void Use(Player p, List<Monster> m)
        {
            const int bleedDamage = 10;
            const int bleedTurns = 2;
            double chance = 0.5;
            Console.WriteLine("你点燃了红磷!");
            Console.WriteLine("烟雾弥漫了整个房间...");
            foreach (Monster monsters in m)
            {
                // 数值与旧的 DOT 10 / 2 回合一一对应；重复使用同一只怪时按流血口径叠加
                // （伤害 min(现有+10, 10)、剩余回合相加）。
                monsters.AddBuff(new BleedBuff(bleedDamage, bleedTurns));
            }
            Console.WriteLine("全部怪物受到" + bleedTurns + "时长的 " + bleedDamage + "伤害!");
            if (Random.Shared.NextDouble() < chance)
            {
                // 原为「p.Block += 1」（次数制，白吃一整刀）；改护盾值制后按 5 点盾给，
                // 与「防御」行动 / 盾牌道具同一档，三个加盾来源数值统一，玩家好记。
                p.AddShield(5);
                Console.WriteLine("烟雾掩护了你！你获得了 5 点护盾");
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
