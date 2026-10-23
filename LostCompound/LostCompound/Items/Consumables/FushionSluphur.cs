using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Consumables
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

            // 环境自己的额外效果：热硫可以堵住水淹洞穴的出水口（设计文档 5.2）。
            // 只通知环境「热硫被用了」，具体做什么、堵几回合由环境决定 ——
            // 原先这里是 `if (p.CurrentEnvironment is FloodedCavern flooded) flooded.PlugOutlet(3)`，
            // 每加一种「某道具在某环境有额外效果」的组合，道具里就要多一个 is。
            p.CurrentEnvironment?.OnFusionSulphurUsed();
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
