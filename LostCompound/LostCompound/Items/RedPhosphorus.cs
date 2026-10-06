using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public class RedPhosphorus : Item, Useable
    {
        public RedPhosphorus(int count)
        {
            this.name = "红磷";
            this.count = count;
            this.id = 31;
            this.maxStack = 99;
            this.Stackable = true;
        }

        public override ItemType? getType()
        {
            return ItemType.RED_PHOSPHORUS;
        }

        public void use(Player p, List<Monster> m)
        {
            int dotDmg = 10;
            int dotdur = 2;
            double chance = 0.5;
            Console.WriteLine("你点燃了红磷!");
            Console.WriteLine("烟雾弥漫了整个房间...");
            foreach (Monster monsters in m)
            {
                monsters.setDOTdur(dotdur);
                monsters.setDOTdmg(dotDmg);
            }
            Console.WriteLine("全部怪物受到" + dotdur + "时长的 " + dotDmg + "伤害!");
            if (Random.Shared.NextDouble() < chance)
            {
                p.block += 1;
                Console.WriteLine("烟雾掩护了你！你获得了一点格挡");
            }
            else
            {
                Console.WriteLine("红磷的烟雾并没有掩护你!");
            }
        }

        public void ExpBattle(Player p)
        {
            Console.WriteLine("你不能在非战斗下使用红磷！");
        }
    }
}
