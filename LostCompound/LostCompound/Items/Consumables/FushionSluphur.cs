using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Consumables
{
    public sealed class FushionSluphur(int count) : Item(ItemIds.FushionSluphur, "热硫", 99, true, count), IChangeable, IUseable, ITurnCount, IItemState
    {
        /// <summary>
        /// 距离消失还剩几场战斗。**必须跨存档保留**：它决定道具还能不能继续用，
        /// 不存的话读档后计时器会重置回 3，等于白送保质期。
        /// </summary>
        public int Timer { get; set; } = 3;

        /// <summary>存档键名。**发布后不能改**。</summary>
        private const string StateKeyTimer = "timer";

        public Dictionary<string, int> SaveState() => new() { [StateKeyTimer] = Timer };

        public void LoadState(IReadOnlyDictionary<string, int> state)
        {
            // 缺键 = 旧存档没这一项，保持构造时的默认值 3（与升级前行为一致）
            if (state.TryGetValue(StateKeyTimer, out int timer))
            {
                // 夹到 [0, 3]：构造时的默认值 3 是它的寿命上限，
                // 负数会让 ShouldRemove 立刻成立（读档瞬间「热硫已消失」）
                Timer = Math.Clamp(timer, 0, 3);
            }
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

        public override ItemType? Type => ItemType.FushionSluphur;

        public void Use(Player p, List<Monster> m)
        {
            Console.WriteLine("====你使用了热硫!====");
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
                monster.TakeAoeDamage(10);
            }
            Count -= 1;

            // 环境自己的额外效果：热硫可以堵住水淹洞穴的出水口（设计文档 5.2）。
            // 只通知环境「热硫被用了」，具体做什么、堵几回合由环境决定 ——
            // 原先这里是 `if (p.CurrentEnvironment is FloodedCavern flooded) flooded.PlugOutlet(3)`，
            // 每加一种「某道具在某环境有额外效果」的组合，道具里就要多一个 is。
            p.CurrentEnvironment?.OnFusionSulphurUsed();
        }

        /// <summary>
        /// 战斗外使用时的解释。原先这里是空的 `{ }` —— 而 IUseable 的这个方法现在真的会被调用
        /// （地图上按 5 打开背包），空着就等于「选了道具，游戏一声不吭」。
        /// 热硫的使用效果是「丢出去炸 / 堵出水口」，都得在战斗里才成立。
        /// </summary>
        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("热硫只能在战斗中使用！");
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
