using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// **秒针虫**（Hyberty 2026-11-22 设计，放射性区域专属）。
    ///
    /// 数值：`hp 75` / `atk 0` / `def 0`，档位 **普通**。
    ///
    /// 技能（唯一）：**不攻击**。只要它还活着，环境辐射的计时速度 **×2**
    ///   （10 秒/格 的毒区变成 5 秒/格 —— 走的是「间隔减半」，不是「伤害翻倍」，
    ///   所以盖革计数器、碘片、换房清零这些规则都原样复用）。
    ///
    /// 设计意图（Hyberty 原话）：它是唯一一个「**不打你，却在杀你**」的怪。
    ///   不杀它，每轮实际耗时 25 秒就多掉 2~3 血；杀它要 2~3 轮，代价约 5~8 血。
    ///   玩家必须做「**先杀谁**」的决策 —— 这正是怪物池里原本缺的那一种判断
    ///   （岩浆怪/食人鱼/盾兵都是「该打/不该打」，不是「优先级」）。
    ///
    /// 为什么这个加速**不写在怪身上**：
    ///   环境辐射的秒数只有 `RadioactiveArea` 一个写入口（它在 `UpdateTracking` 里统一算间隔）。
    ///   怪只需要「报一声我在场」（<see cref="IncreasesRadiationSpeed"/>），
    ///   乘不乘、乘多少由环境决定 —— 这样「谁在改秒数」永远只有一个地方。
    /// </summary>
    public sealed class SecondHandWorm : Monster
    {
        /// <summary>档位：普通。</summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        /// <summary>血量（Hyberty：75）。</summary>
        public const double BaseHp = 75;

        public const double BaseAtk = 0;
        public const double BaseDef = 0;
        public const double BaseStr = 0;

        /// <summary>它在场时把辐射间隔**除以**这个数（Hyberty：2 → 间隔减半）。</summary>
        public const int RadiationSpeedMultiplier = 2;

        public SecondHandWorm()
        {
            Type = MonsterType.SecondHandWorm;
            Name = "秒针虫";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = BaseStr;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        /// <summary>
        /// 告诉环境：它在场，辐射要加速。
        /// 环境侧读它（见 `RadioactiveArea.UpdateTracking`），这里是**唯一**的声明处。
        /// </summary>
        public int RadiationSpeedFactor => RadiationSpeedMultiplier;

        /// <summary>
        /// 它**不攻击** —— 这个方法是空的，而且必须存在：`Monster.Attack` 是抽象成员。
        /// 基类把它当作「伤害骨架」的入口，但秒针虫的伤害是**间接**的
        /// （通过把环境辐射的秒数减半），所以这里什么都不做是对的，不是漏写。
        /// </summary>
        public override void Attack(Player p) { }

        /// <summary>它不攻击 —— 但**不能**留空：怪物回合会照常跑，只是什么都不做。</summary>
        public override void OnMonsterTurn(Player p)
        {
            Console.WriteLine(Name + "咔哒咔哒地走着，没有碰你。");
        }

        /// <summary>
        /// 意图显示成「加速」而不是「打 0」——
        /// 写成「打 0」玩家会以为它是个废怪，看不出它才是真正的威胁。
        /// </summary>
        public override string IntentText() => "加速×" + RadiationSpeedMultiplier;

        /// <summary>受到伤害：走基类骨架（恒等预处理 → DEF 减免 → 护盾）。没有特殊抗性。</summary>
        public override void TakeDamage(double dmg) => TakeDamageWithDefaultLog(dmg);

        /// <summary>护盾吃光整刀时的播报。</summary>
        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "的护盾挡下了伤害！（剩余护盾:" + Shield.ToString("F1") + "）");
        }

        /// <summary>实际扣血时的播报。</summary>
        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到" + hpLoss.ToString("F1") + "点伤害!");
        }

        /// <summary>没有多技能，但基类要求实现；按「返回 0..count-1」的约定实现。</summary>
        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);
    }
}
