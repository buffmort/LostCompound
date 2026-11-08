using LostCompound.Buffs;
using LostCompound.Buffs.Types;
using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Elite
{
    /// <summary>
    /// **木乃伊**（设计文档 §6「地图 3 · 沙漠」精英怪，Hyberty 2026-11-08 定）。
    ///
    /// 数值：`hp 100` / `def 10` / `atk 3` / `str 0`，档位 **精英**。
    ///
    /// 两条机制：
    ///   ① **怕磷**：被**磷系 / 火系**道具打中时中「磷灼烧」
    ///      （<see cref="PhosphorusBurn"/>：每回合 10 点、3 回合，**期间 DEF 变成 0**）。
    ///      普通刀砍不会触发 —— 它只怕特定来源的伤害。
    ///   ② **每回合掷一次**：50% 普通攻击（3 点），50% 给自己上 **50 层易伤**
    ///      （<see cref="Vulnerability"/>：受伤 ×(1 + 0.01×层数)，50 层 = 1.5 倍）。
    ///      所以它会「越站越脆」，但也因此更值得你多打它几刀。
    ///
    /// 为什么上易伤是**给自己**上的（Hyberty 裁定）：这是它的「自残式强化」设定 ——
    /// 上了易伤它并没有变强，只是变得更好打。玩家因此会希望它多上几次易伤。
    /// </summary>
    public sealed class Mummy : Monster
    {
        public override MonsterRank Rank => MonsterRank.Elite;

        public const double BaseHp = 100;
        public const double BaseDef = 10;
        public const double BaseAtk = 3;

        /// <summary>每回合发动攻击的概率；其余概率用来上易伤（裁定：各 50%）。</summary>
        public const double AttackChance = 0.5;

        /// <summary>一次「上 buff」给自己多少层易伤（裁定：50 层）。</summary>
        public const int VulnerabilityStacks = 50;

        public Mummy()
        {
            Type = MonsterType.Mummy;
            Name = "木乃伊";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = 0;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        /// <summary>
        /// 防御：**中了磷灼烧就变 0**，否则是基础值 10。
        ///
        /// 为什么写成计算属性而不是在 buff 里改字段再改回来：
        ///   buff 有到期 / 战斗结束 / 将来驱散三条移除路径，漏一条就会永久偏移；
        ///   写成「按当前状态算」之后，挂上自动生效、消失自动恢复，零还原代码。
        /// </summary>
        public override double Def => HasPhosphorusBurn ? 0 : base.Def;

        /// <summary>身上有没有磷灼烧（决定 DEF 是否为 0）。</summary>
        private bool HasPhosphorusBurn
        {
            get
            {
                foreach (Buff b in Buffs)
                {
                    if (b is PhosphorusBurn && !b.IsExpired) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 受伤预处理：把「易伤」的倍率乘上去。
        ///
        /// 位置在 DEF 减免**之前**（与岩浆怪的高温免疫同一个位置）：
        /// 易伤的说法是「承受的伤害 ×1.5」，作用对象是伤害本身，
        /// 所以先乘、再走 DEF 减法公式才是对的。
        /// </summary>
        protected override double ProcessIncomingDamage(double incomingDamage)
        {
            foreach (Buff b in Buffs)
            {
                if (b is Vulnerability v && !b.IsExpired)
                {
                    return incomingDamage * v.DamageMultiplier;
                }
            }
            return incomingDamage;
        }

        /// <summary>
        /// 被磷系 / 火系打中 → 中磷灼烧（每回合 10、3 回合、DEF 归零）。
        /// 其它来源（普通攻击、AOE 里的非磷道具）什么都不发生。
        /// </summary>
        public override void OnElementalHit(DamageElement element)
        {
            if (element is not (DamageElement.Phosphorus or DamageElement.Fire)) return;
            if (!IsAlive) return;

            AddBuff(new PhosphorusBurn());
            Console.WriteLine(Name + "被磷火烧了起来！接下来 3 回合每回合 -10 血，防御归零。");
        }

        /// <summary>受到伤害：走基类骨架（预处理里已乘上易伤），再做 DEF 减免与护盾。</summary>
        public override void TakeDamage(double dmg) => TakeDamageWithDefaultLog(dmg);

        /// <summary>每回合掷一次：50% 攻击 / 50% 给自己上易伤。</summary>
        public override void OnMonsterTurn(Player p)
        {
            if (Random.Shared.NextDouble() < AttackChance)
            {
                Attack(p);
                return;
            }

            AddBuff(new Vulnerability(VulnerabilityStacks));
            Console.WriteLine(Name + "裹尸布下渗出黄沙，它身上浮现出裂纹（易伤 +" +
                              VulnerabilityStacks + " 层，之后受伤更痛）。");
        }

        /// <summary>普通攻击（str 0，伤害就是固定的 3 点）。</summary>
        public override void Attack(Player p)
        {
            Console.WriteLine(Name + "用缠满布条的手抓向你！");
            p.TakeDamage(Atk);
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
