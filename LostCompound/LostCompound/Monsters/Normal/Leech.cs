using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// **水蛭**（设计文档 §6「地图 2 · 水淹洞穴」专属怪，Hyberty 2026-11-08 定）。
    ///
    /// 数值：`hp 10`（**初始**）/ `maxHp 200`（吸血能把它养到这个上限）/ `def 0` / `atk 5` / `str 1`，档位 **普通**。
    ///
    /// 行为（每回合掷一次）：
    ///   · **70% 攻击**：造成 5 点伤害，然后**吸走玩家实际掉血量的 70%** 回补自己（困难模式 100%，难度系统未做）；
    ///   · **30% 不动**：这一回合什么都不做。
    ///
    /// 为什么吸血按「玩家实际掉的血」算，而不是按名义攻击力 5：
    ///   Hyberty 裁定口径是「吸取伤害的血量」。按名义值算的话，玩家叠了 DEF 和盾、
    ///   实际一滴血没掉，水蛭照样能回 3.5 —— 那就不叫吸血，叫凭空回血。
    ///   所以 <see cref="Player.TakeDamage"/> 现在会返回真实掉血量，这里直接用它的返回值：
    ///   被盾完全挡下时返回 0，水蛭就吸不到东西（会打一行提示，不让玩家以为是 bug）。
    ///
    /// 设计意图：它开局只有 10 血（一刀就能砍死），但只要放着不管，靠吸血能涨到 200。
    ///   所以玩家面对它的决策是「**赶紧先杀它**」，而不是先清别的怪 ——
    ///   这正是水淹洞穴那张图想要的节奏。
    /// </summary>
    public sealed class Leech : Monster
    {
        /// <summary>档位：普通。</summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        /// <summary>初始血量（Hyberty：10）。</summary>
        public const double BaseHp = 10;

        /// <summary>血量上限（Hyberty：200）—— 吸血最多回补到这里。</summary>
        public const double MaxHpCap = 200;

        public const double BaseDef = 0;
        public const double BaseAtk = 5;
        public const double BaseStr = 1;

        /// <summary>吸血比例：吸走「玩家实际掉血量」的多少来回补自己（Hyberty：0.7；困难 1.0 待难度系统）。</summary>
        public const double DrainRatio = 0.7;

        /// <summary>每回合发动攻击的概率（Hyberty：0.7 攻击 / 0.3 不动）。</summary>
        public const double AttackChance = 0.7;

        public Leech()
        {
            Type = MonsterType.Leech;
            Name = "水蛭";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = BaseStr;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        /// <summary>
        /// 一次攻击：先掷「动不动」，动了就造成伤害并按**实际掉血量**吸血。
        ///
        /// 注意吸血是**回补自己**（`Hp` 上涨，上限 <see cref="MaxHpCap"/>），
        /// 不是给玩家挂 debuff —— 所以走的完全是怪物自己的血量，与 buff 系统无关。
        /// </summary>
        public override void Attack(Player p)
        {
            if (Random.Shared.NextDouble() >= AttackChance)
            {
                Console.WriteLine(Name + "蜷缩着，没有动弹。");
                return;
            }

            // 返回值 = 玩家实际掉的血（被盾全挡下时是 0）
            double hpLost = p.TakeDamage(Atk);
            double drain = hpLost * DrainRatio;

            if (drain <= 0)
            {
                Console.WriteLine(Name + "扑了个空，一滴血也没吸到。");
                return;
            }

            double before = Hp;
            Hp = Math.Min(MaxHpCap, Hp + drain);
            Console.WriteLine(Name + "吸走了你 " + drain.ToString("F1") + " 点血（自身 " +
                              before.ToString("F1") + " → " + Hp.ToString("F1") + "）");
        }

        /// <summary>怪物回合：掷一次，攻击或不动。</summary>
        public override void OnMonsterTurn(Player p)
        {
            Attack(p);
        }

        /// <summary>受到伤害：走基类骨架（恒等预处理 → DEF 减免 → 护盾）。水蛭没有特殊抗性。</summary>
        public override void TakeDamage(double dmg) => TakeDamageWithDefaultLog(dmg);

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
