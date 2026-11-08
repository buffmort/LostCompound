using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// **沙虫**（设计文档 §6「地图 3 · 沙漠」专属怪，Hyberty 2026-11-08 定）。
    ///
    /// 数值：`hp 30` / `def 0` / `atk 8`，档位 **普通**。
    ///
    /// 核心机制是**遁地**：
    ///   · 开打就在沙里（<see cref="IsBurrowed"/> = true），**一直遁着**；
    ///   · 遁地期间：
    ///       ① **单体攻击打不中它**（玩家的普通攻击 / 单点道具全部落空，白费一回合）；
    ///       ② 只有 **AOE**（热硫、毒气弹……走 <see cref="Monster.TakeAoeDamage"/>）
    ///          和**采沙**能对付它；
    ///       ③ 它自己的攻击伤害 **×2**（8 → 16）。
    ///   · 玩家用**战斗菜单第 6 项「采沙」**挖一次之后，沙虫被翻出沙面、
    ///     **永久**露在外面（<see cref="OnSandDug"/>）：此后单体攻击能打中，伤害回到 8。
    ///
    /// 为什么「采沙」不造成伤害（Hyberty 裁定）：采沙是**行动**不是攻击。
    ///   它买的不是这一下的伤害，而是「把沙虫变成可被单体攻击的目标」这个状态。
    ///   于是这场战斗的解法是：先花一回合采沙，再正常砍 —— 而不是靠堆 AOE 道具。
    ///
    /// 掉落：**沙子 15~30 个**（Hyberty 定）。沙漠本来就只出沙子，所以这条不破坏 §5.3 的口径。
    /// </summary>
    public sealed class Sandworm : Monster
    {
        /// <summary>档位：普通。</summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        public const double BaseHp = 30;
        public const double BaseDef = 0;
        public const double BaseAtk = 8;

        /// <summary>遁地时的攻击伤害倍率（Hyberty：每回合伤害 ×2）。</summary>
        public const double BurrowDamageMultiplier = 2.0;

        /// <summary>掉落沙子数量范围（Hyberty：15~30，含两端）。</summary>
        public const int MinLootSand = 15;
        public const int MaxLootSand = 30;

        /// <summary>是否还在沙里。开局为 true，采沙一次后永久变 false。</summary>
        public bool IsBurrowed { get; private set; } = true;

        public Sandworm()
        {
            Type = MonsterType.Sandworm;
            Name = "沙虫";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = 0;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        /// <summary>
        /// 攻击：遁地时伤害翻倍（8 → 16），露出来之后是正常的 8。
        /// 没有技能、没有掷骰子 —— 它每回合都咬。
        /// </summary>
        public override void Attack(Player p)
        {
            double dmg = Atk * (IsBurrowed ? BurrowDamageMultiplier : 1.0);
            if (IsBurrowed)
            {
                Console.WriteLine(Name + "从沙下窜出，狠狠咬了你一口！（遁地伤害翻倍）");
            }
            else
            {
                Console.WriteLine(Name + "扭动着身子咬了你一口！");
            }
            p.TakeDamage(dmg);
        }

        /// <summary>
        /// **单体伤害**：遁地时打不中。
        ///
        /// 注意这里不是「减伤」而是**完全不结算** —— 连盾都不碰、也不播报受伤，
        /// 因为攻击根本没有落到它身上。
        /// </summary>
        public override void TakeDamage(double dmg)
        {
            if (IsBurrowed)
            {
                Console.WriteLine("你的攻击打在沙地上，扬起一片尘土 —— " + Name + "在沙子深处。");
                return;
            }
            TakeDamageWithDefaultLog(dmg);
        }

        /// <summary>
        /// **AOE 伤害**：无视遁地，正常结算（这是遁地期间的唯二解法之一）。
        /// 覆写基类的转发默认实现，正是本类存在的关键。
        /// </summary>
        public override void TakeAoeDamage(double dmg) => TakeDamageWithDefaultLog(dmg);

        /// <summary>
        /// 玩家采了一次沙：把它从沙里翻出来，**永久**（不会重新钻回去）。
        /// </summary>
        public override void OnSandDug()
        {
            if (!IsBurrowed) return;
            IsBurrowed = false;
            Console.WriteLine(Name + "被从沙里翻了出来，暴露在外！");
        }

        /// <summary>怪物回合：必定攻击。</summary>
        public override void OnMonsterTurn(Player p)
        {
            Attack(p);
        }

        /// <summary>护盾吃光整刀时的播报。</summary>
        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "的护盾挡下了伤害！（剩余护盾:" + Shield.ToString("F1") + "）");
        }

        /// <summary>实际扣血时的播报。</summary>
        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
        }

        /// <summary>没有多技能，但基类要求实现；按「返回 0..count-1」的约定实现。</summary>
        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);
    }
}
