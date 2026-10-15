using LostCompound.Buffs;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 毒气弹（ID 26，消耗品，堆叠 10）。
    ///
    /// 文档依据：DESIGN 第九章「26 毒气弹 消耗品 10 热硫 + 白磷 + 玻璃罐 |
    ///   1（困难 2）回合 AOE 流血 25（困难 20）+ 自伤 1（困难 2），
    ///   10% 对非 boss 敌人造成 2147483647 伤害」。
    ///
    /// 已实现：全体怪物挂流血（伤害 25、1 回合）+ 玩家自伤 1。
    ///
    /// ⚠️ 两条**故意没做**的，理由写在下面（都属于「不报错、不崩溃，只是数值/效果不对」的那类，
    ///   所以必须留文字，不能让后来者以为做过了）：
    ///
    /// 1. **困难模式数值（2 / 20 / 2）一律用普通模式值** —— 项目里还没有难度系统
    ///    （这是本项目一贯做法：BleedBuff 的类注释、碘片、红磷等处都是同一口径）。
    ///    TODO：难度系统落地后，此处按难度把回合数 1→2、流血伤害 25→20、自伤 1→2。
    ///
    /// 2. **「10% 对非 boss 敌人造成 int.MAX_VALUE 伤害」没有实现** —— 因为代码里
    ///    **没有任何「Boss」标记**可用：
    ///      · <see cref="MonsterType"/> 只有 Dragon / Dummy / LavaSlime / TwistedPair1 /
    ///        TwistedPair2 / Zombie，是「怪物种类」枚举，不含「是不是 Boss」这一位
    ///        （文档 6.1 把 Dragon 与 TwistedPair1/2 归为 Boss，但代码里没有表达）；
    ///      · <see cref="Monster"/> 基类也没有 IsBoss 之类的属性；
    ///      · <c>RoomType.Boss</c> 只标房间，而且 <c>Use(Player, List&lt;Monster&gt;)</c>
    ///        拿到的只有怪物列表，拿不到房间。
    ///    按「有就用、没有就报告，不要自己新造概念」的约定，这里不硬编码
    ///    「Dragon/TwistedPair 就是 Boss」，也不新加 Monster.IsBoss（那是给所有怪物引入一个新概念）。
    ///    TODO：等 Boss 标记定下来后，在这段循环里补
    ///      「10% 概率、且该怪不是 Boss → 直接 <c>mon.TakeDamage(int.MaxValue)</c>」。
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

        public void Use(Player p, List<Monster> m)
        {
            Console.WriteLine("====你丢出了毒气弹!====");
            Calculator.Delay(250);

            foreach (Monster monster in m)
            {
                // 走 AddBuff（同类叠加由 Buff.MergeFrom 统一处理），不自己判断已有流血。
                monster.AddBuff(new BleedBuff(BleedDamage, BleedTurns));
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
