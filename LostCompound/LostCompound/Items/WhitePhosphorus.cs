using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public class WhitePhosphorus : Item, Useable, Changeable
    {
        public WhitePhosphorus(int count)
        {
            this.name = "白磷";
            this.id = 5;
            this.maxStack = 99;
            this.Stackable = true;
            this.count = count;
        }

        public override ItemType? getType()
        {
            return ItemType.WHITE_PHOSPHORUS;
        }

        public void use(Player p, List<Monster> m)
        {
            double dotDmg = 20;
            int DUR = 3;
            Console.WriteLine("====你使用了白磷!====");
            Calculator.delay(250);
            int choice = p.selectTarget(m);
            if (choice == -1)
            {
                Console.WriteLine("你输入了错误的序号 跳过");
            }
            Monster target = m[choice];
            target.setDOTdmg(dotDmg);
            target.setDOTdur(DUR);
            Console.WriteLine("你向" + target.getName() + "投掷了一个白磷!造成" + target.getDOTdur() + "回合持续时间" +
                    "与 " + target.getDOTdmg() + "每回合伤害");
            foreach (Monster monster in m)
            {
                if (monster == target)
                {
                    continue;
                }
                monster.setDOTdmg(10);
                monster.setDOTdur(3);
            }
            Console.WriteLine("你对所有怪物造成10点溅射伤害!");
            count -= 1;
        }

        public void ExpBattle(Player p)
        {
            Console.WriteLine("你不能单独用他!");
        }

        public bool ShouldRemove()
        {
            return count == 0;
        }

        public Item Become()
        {
            return null;
        }
    }
}
