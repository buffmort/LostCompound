using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public sealed class FushionSluphur(int count) : Item(ItemIds.FushionSluphur, "热硫", 99, true, count), IChangeable, IUseable, ITurnCount
    {
        public int Timer { get; set; } = 3;

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

        public override ItemType? Type => ItemType.FushionSluphur;

        public void Use(Player p, List<Monster> m)
        {
            Console.WriteLine("====你使用了热硫!====");
            Calculator.Delay(250);
            int choice = p.SelectTarget(m);
            if (choice == -1)
            {
                Console.WriteLine("你输入了错误的对象代号!");
                return;
            }
            Monster target = m[choice];
            target.AddSkipTurn(1);
            Console.WriteLine("你给" + target + "造成了眩晕 他跳过了一回合!");
            foreach (Monster monster in m)
            {
                monster.TakeDamage(10);
            }
            Count -= 1;
        }

        public void ExplainBattleOnly(Player p)
        {

        }

        public void OnBattleStart(Player p, EnvType envType)
        {
            Timer -= 1;
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {

        }
    }
}
