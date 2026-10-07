using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public sealed class WhitePhosphorus(int count) : Item(ItemIds.WhitePhosphorus, "白磷", 99, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.WhitePhosphorus;

        public void Use(Player p, List<Monster> m)
        {
            double dotDmg = 20;
            int DUR = 3;
            Console.WriteLine("====你使用了白磷!====");
            Calculator.Delay(250);
            int choice = p.SelectTarget(m);
            if (choice == -1)
            {
                Console.WriteLine("你输入了错误的序号 跳过");
                return;   // 修复：原先缺少 return，会继续执行下面的 m[-1] 导致越界崩溃
            }
            Monster target = m[choice];
            target.AddDotDamage(dotDmg);
            target.AddDotDuration(DUR);
            Console.WriteLine("你向" + target.Name + "投掷了一个白磷!造成" + target.DotDuration + "回合持续时间" +
                    "与 " + target.DotDamage + "每回合伤害");
            foreach (Monster monster in m)
            {
                if (monster == target)
                {
                    continue;
                }
                monster.AddDotDamage(10);
                monster.AddDotDuration(3);
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
