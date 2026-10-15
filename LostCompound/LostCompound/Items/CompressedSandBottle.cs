using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 压缩沙瓶（ID 29，消耗品，堆叠 1）。
    ///
    /// 文档依据（DESIGN 第九章）：「29 压缩沙瓶 消耗品 1 20 沙子 + 玻璃瓶 |
    ///   跳过 2 回合并且击杀血量 &lt; 玩家血量 × 0.5 的怪物」。
    ///
    /// 两条效果的对象都取**当前房间的全部怪物**（文档是一句话写的、没有「选一只」的动作，
    /// 与 AOE 类道具红磷/毒气弹同一口径）：
    ///   1. 每只怪 <see cref="Monster.AddSkipTurn"/>(2) —— 「跳过 2 回合」指的是**跳过敌人**
    ///      （若是跳过玩家，那句「并且击杀…怪物」的收益方就自相矛盾了；且
    ///      Player.AddSkipTurn 会让玩家白挨两轮，与「压缩沙瓶是强势清场道具」的定位相反）。
    ///      用 AddXxx 而不是裸写 = 2：SkipTurn 是**累加**语义（CONVENTIONS 2.3）。
    ///   2. 血量低于「玩家当前血量 × 0.5」的怪直接处死：写 <c>Hp = 0</c>，与项目里既有的
    ///      「直接改血量」写法一致（<see cref="Monsters.LavaSlime.OnWaterPoured"/> 扣 70% 血
    ///      也是直接 <c>Hp -= loss</c>）。**不走 TakeDamage 是有意的**：
    ///      TakeDamage 会过 DEF 减免与护盾，一个「斩杀」阈值会被防御公式削掉，
    ///      变成「判定时低于阈值、结算后没死」。置 0 之后由战斗循环的
    ///      <c>Tools.RemoveDeadMonsters</c> 统一收尸（掉落照发），不会漏。
    ///
    /// ⚠️ 文档里的「玻璃瓶」在项目里没有这个道具，玻璃容器只有**玻璃罐（ID 7）**，
    ///   故配方按 <see cref="ItemIds.GlassJar"/> 写；这是替用户做的解释，待复核。
    /// </summary>
    public sealed class CompressedSandBottle(int count) : Item(ItemIds.CompressedSandBottle, "压缩沙瓶", 1, false, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.CompressedSandBottle;

        /// <summary>让怪物跳过的回合数（文档：2）。</summary>
        public const int SkipTurns = 2;

        /// <summary>斩杀阈值系数：怪物血量 &lt; 玩家血量 × 该系数即被处死（文档：0.5）。</summary>
        public const double ExecuteThresholdRatio = 0.5;

        public void Use(Player p, List<Monster> m)
        {
            Console.WriteLine("====你砸开了压缩沙瓶!====");
            Calculator.Delay(250);

            if (m.Count == 0)
            {
                Console.WriteLine("这里没有可以作用的对象！");
                return;
            }

            // 阈值按「施放瞬间」的玩家血量算一次，全程不再变 ——
            // 别在循环里重复读 p.Hp，否则将来若某只怪的反击改了玩家血量，
            // 后面的怪会用不同的阈值判定（同一个道具打出两种标准）。
            double threshold = p.Hp * ExecuteThresholdRatio;
            int executed = 0;

            foreach (Monster monster in m)
            {
                if (!monster.IsAlive) continue;

                monster.AddSkipTurn(SkipTurns);

                if (monster.Hp < threshold)
                {
                    // 直接处死；收尸与掉落交给 Tools.RemoveDeadMonsters（统一通路）。
                    monster.Hp = 0;
                    executed++;
                    Console.WriteLine(monster.Name + " 被压碎了！");
                }
            }

            Console.WriteLine("压缩沙尘爆开：全部敌人跳过 " + SkipTurns + " 回合，" +
                              executed + " 只残血敌人被直接处死。");

            Count -= 1;
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("压缩沙瓶只能在战斗中使用！");
        }

        /// <summary>用光即移出背包（CONVENTIONS 2.4）。</summary>
        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}
