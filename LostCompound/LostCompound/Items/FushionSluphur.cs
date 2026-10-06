using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public class FushionSluphur : Item, Changeable, Useable, TurnCount
    {
        public int Timer = 3;

        public FushionSluphur(int count)
        {
            this.name = "热硫";
            this.id = 4;
            this.maxStack = 99;
            this.Stackable = true;
            this.count = count;
        }

        public bool ShouldRemove()
        {
            if (Timer > 0)
            {
                Console.WriteLine("热硫将在" + Timer + "回合后消失!");
            }
            else
            {
                Console.WriteLine("热硫已消失!");
            }
            return Timer <= 0;
        }

        public Item Become()
        {
            return null;
        }

        public override ItemType? getType()
        {
            return ItemType.FushionSluphur;
        }


        public void use(Player p, List<Monster> m)
        {
            Console.WriteLine("====你使用了热硫!====");
            Calculator.delay(250);
            int choice = p.selectTarget(m);
            if (choice == -1)
            {
                Console.WriteLine("你输入了错误的对象代号!");
                return;
            }
            Monster target = m[choice];
            target.addSkipTurn(1);
            Console.WriteLine("你给" + target + "造成了眩晕 他跳过了一回合!");
            foreach (Monster monster in m)
            {
                monster.takeDmg(10);
            }
            count -= 1;
        }


        public void ExpBattle(Player p)
        {

        }

        public void onBattleStart(Player p, EnvType envType)
        {
            Timer -= 1;
        }

        public void onBattleEnd(Player p, EnvType envType)
        {

        }
    }
}
