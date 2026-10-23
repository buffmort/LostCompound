using LostCompound.Buffs;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Consumables
{
    /// <summary>
    /// 毒气弹（ID 26，消耗品，堆叠 10）。
    ///
    /// 文档依据：DESIGN 第九章「26 毒气弹 消耗品 10 热硫 + 白磷 + 玻璃罐 |
    ///   1（困难 2）回合 AOE 流血 25（困难 20）+ 自伤 1（困难 2），
    ///   10% 对非 boss 敌人造成 2147483647 伤害」。
    ///
    /// 已实现：全体怪物挂流血（伤害 25、1 回合）+ 玩家自伤 1
    ///   + **10% 对非 Boss 敌人造成 <c>int.MAX_VALUE</c> 伤害**（Boss 判定走 <see cref="MonsterRank"/>）。
    ///
    /// ⚠️ 一条**故意没做**的，理由写在下面（属于「不报错、不崩溃，只是数值不对」的那类，
    ///   所以必须留文字，不能让后来者以为做过了）：
    ///
    /// 1. **困难模式数值（2 / 20 / 2）一律用普通模式值** —— 项目里还没有难度系统
    ///    （这是本项目一贯做法：BleedBuff 的类注释、碘片、红磷等处都是同一口径）。
    ///    TODO：难度系统落地后，此处按难度把回合数 1→2、流血伤害 25→20、自伤 1→2。
    ///
    /// 顺带记一个既有缺口：流血每回合伤害上限是 10（见 <see cref="BleedBuff.MaxDamagePerTurn"/>），
    /// 所以这里挂的 25 会被夹到 10 —— 与白磷 20 被夹到 10 是同一个既有问题，本次不顺手改。
    /// </summary>
    public sealed class GasBomb(int count) : Item(ItemIds.GasBomb, "毒气弹", 10, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.GasBomb;

        /// <summary>AOE 流血：每回合伤害（文档 25，困难 20）。</summary>
        public const int BleedDamage = 25;

        /// <summary>AOE 流血持续回合（文档 1，困难 2）。</summary>
        public const int BleedTurns = 1;

        /// <summary>使用后的自伤（文档 1，困难 2）。</summary>
        public const double SelfDamage = 1;

        /// <summary>
        /// 「10% 对非 boss 敌人造成 int.MAX_VALUE 伤害」的触发概率（文档 26）。
        /// 抽出来当常量：它是要写进文档的数值，不该散在循环里。
        /// </summary>
        public const double NonBossExecuteChance = 0.10;

        public void Use(Player p, List<Monster> m)
        {
            Console.WriteLine("====你丢出了毒气弹!====");
            Calculator.Delay(250);

            foreach (Monster monster in m)
            {
                // 走 AddBuff（同类叠加由 Buff.MergeFrom 统一处理），不自己判断已有流血。
                monster.AddBuff(new BleedBuff(BleedDamage, BleedTurns));

                // 文档 26：「10% 对非 boss 敌人造成 int.MAX_VALUE 伤害」。
                //
                // 「是不是 Boss」由 MonsterRank 回答（Monster.Rank，见 Monsters\MonsterRank.cs）——
                // 这里刻意**不去认具体类型**（不写 `is Dragon || is TwistedPairP1`）：
                // 那样每加一个 Boss 都要回头改这个道具，而且「龙与双子是 Boss」这件事会散在
                // 道具里、与怪物自己的声明分家（CONVENTIONS 2.5 / 2.7 的能力位判据同理）。
                // 判定用「!= Boss」而不是「== Normal」：将来补齐「精英」档位时，
                // 精英（文档 §6 的「类型：精英」）在文档里同样属于「非 boss」，应当继续吃这一发。
                //
                // 用 TakeDamage 而不是直接 Hp = 0：走统一通路能保留各怪物自己的减免/护盾结算与播报
                // （与压缩沙瓶的斩杀口径不同 —— 那边文档写的就是「击杀」，见其类注释）。
                if (monster.Rank != MonsterRank.Boss &&
                    Random.Shared.NextDouble() < NonBossExecuteChance)
                {
                    Console.WriteLine("毒气弹击穿了" + monster.Name + "！");
                    monster.TakeDamage(int.MaxValue);
                }
            }
            Console.WriteLine("毒气弥漫：全部怪物获得 " + BleedTurns + " 回合的 " + BleedDamage + " 点流血！");

            // 自伤：走 TakeDamage 而不是 Hp -= 1 —— DEF 减免、护盾吸收、状态栏刷新、
            // 死亡判定全在那一条通路上，绕开它就会出现「同样是掉血、行为却不一致」的静默偏差
            // （这与 BleedBuff 的取舍一致）。
            p.TakeDamage(SelfDamage);

            Count -= 1;
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("毒气弹只能在战斗中使用！");
        }

        /// <summary>用光即移出背包（CONVENTIONS 2.4：Use 里扣了 Count 就必须实现 IChangeable）。</summary>
        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}
