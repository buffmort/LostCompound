using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Buffs;

namespace LostCompound.Items
{
    public sealed class WhitePhosphorus(int count) : Item(ItemIds.WhitePhosphorus, "白磷", 99, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.WhitePhosphorus;

        public void Use(Player p, List<Monster> m)
        {
            // 数值沿用原代码的字面量（主目标 20、溅射 10，均为 3 回合）——
            // 设计文档第九章道具表写的是「DOT 30 + 溅射 10」，两者本来就不一致，
            // 这次只做 DOT → 流血 的机制替换，没有顺手改数值（缺口见报告）。
            // 注意：流血每回合伤害上限 10，所以主目标的 20 会被夹到 10。
            const int damage = 20;
            const int DUR = 3;
            Console.WriteLine("====你使用了白磷!====");
            Calculator.Delay(250);
            int choice = p.SelectTarget(m);
            if (choice == -1)
            {
                Console.WriteLine("你输入了错误的序号 跳过");
                return;   // 修复：原先缺少 return，会继续执行下面的 m[-1] 导致越界崩溃
            }
            Monster target = m[choice];
            target.AddBuff(new BleedBuff(damage, DUR));
            // 播报用「叠加后的实际值」而不是刚传进去的字面量：同一只怪已有流血时，
            // 伤害会被夹取、剩余回合会相加，直接打印字面量会与实际结算不符。
            // （这一点与旧代码打印 target.DotDuration / target.DotDamage 的口径一致。）
            BleedBuff applied = target.Buffs.OfType<BleedBuff>().First();
            Console.WriteLine("你向" + target.Name + "投掷了一个白磷!造成" + applied.RemainingTurns + "回合持续时间" +
                    "与 " + applied.Stacks + "每回合伤害");
            foreach (Monster monster in m)
            {
                if (monster == target)
                {
                    continue;
                }
                monster.AddBuff(new BleedBuff(10, DUR));
            }
            Console.WriteLine("你对所有怪物造成10点溅射伤害!");
            Count -= 1;
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("你不能单独用他!");
        }

        public bool ShouldRemove()
        {
            return Count == 0;
        }

        public Item Become()
        {
            return null;
        }
    }
}
