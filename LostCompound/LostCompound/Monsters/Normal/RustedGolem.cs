using LostCompound.Buffs;
using LostCompound.Buffs.Types;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// **锈铁傀儡**（通用池怪，第 2~5 层，Hyberty 2026-11-08 由助手设计）。
    ///
    /// 设计目的：补上第 4~5 层的**通用怪空档** —— 僵尸与骷髅只到第 3 层，
    /// 那之后通用池只剩盾兵（一只纯辅助、自己不打人的怪），常规战斗全靠环境专属怪撑着。
    ///
    /// 数值：`hp 30` / `def 5` / `atk 6` / `str 0`，档位 **普通**。
    ///
    /// 主题是**氧化**，机制做成「双向生锈」：
    ///   ① **它自己越打越脆**：每回合结束时自身 DEF -1（最低 0）——
    ///      拖着不打，它自己会锈成一堆废铁，这是给玩家的「耐心回报」；
    ///   ② **它让你的护甲生锈**：每次命中给你挂/加深「锈蚀」
    ///      （<see cref="Rusted"/>：每层 -1 DEF、3 回合、最多 5 层）。
    ///
    /// 于是这场战斗是一场**竞速**：玩家越肉，被锈掉的越多；拖得越久，它自己也越脆。
    /// 这也是全项目第一只**针对玩家 DEF** 的怪 —— 第 2~5 层玩家通常已经堆起防御
    /// （盔甲 +10、盾），需要一只怪来提醒「防御不是免费的」。
    ///
    /// ⚠️ 关于叠层：全局规则是「同类 buff 层数取最高」，所以**不能**靠反复挂 1 层来累加。
    ///    本类自己记「命中了几次」（<see cref="_hitsLanded"/>），第 n 次就挂 n 层 ——
    ///    在「取最高」的规则下依然能逐次加深，到 5 层封顶。
    /// </summary>
    public sealed class RustedGolem : Monster
    {
        /// <summary>档位：普通。</summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        public const double BaseHp = 30;
        public const double BaseDef = 5;
        public const double BaseAtk = 6;

        /// <summary>每回合自己掉多少 DEF（氧化）。</summary>
        public const double DefRustPerTurn = 1.0;

        /// <summary>命中几次之后锈蚀封顶（与 <see cref="Rusted.MaxLayers"/> 一致）。</summary>
        public const int MaxRustLayers = Rusted.MaxLayers;

        /// <summary>这场战斗里它命中过玩家几次 —— 决定给玩家挂几层锈蚀。</summary>
        private int _hitsLanded;

        public RustedGolem()
        {
            Type = MonsterType.RustedGolem;
            Name = "锈铁傀儡";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = 0;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        /// <summary>
        /// 普通攻击：造成 6 点伤害，然后给玩家挂/加深锈蚀。
        ///
        /// 顺序是「先打、再挂」：这一下的伤害按**被打之前**的 DEF 算（玩家还没被锈），
        /// 从下一次挨打开始才吃亏 —— 否则同一回合内既扣血又吃 DEF 削减会显得莫名其妙。
        /// </summary>
        public override void Attack(Player p)
        {
            p.TakeDamage(Atk);

            _hitsLanded++;
            int layers = Math.Min(_hitsLanded, MaxRustLayers);
            p.AddBuff(new Rusted(layers));
            Console.WriteLine(Name + "的锈屑钻进你的护甲缝隙（锈蚀 " + layers +
                              " 层：DEF -" + layers + "，3 回合）。");
        }

        /// <summary>
        /// 怪物回合：先锈自己（DEF -1，最低 0），再攻击。
        ///
        /// 为什么「自己先锈」写在回合开头：玩家看到的是「傀儡又掉了一层锈」，
        /// 然后才挨打 —— 这样它越来越脆这件事在日志里是可读的，
        /// 玩家能判断「再撑两回合它就没防御了」。
        /// </summary>
        public override void OnMonsterTurn(Player p)
        {
            if (Def > 0)
            {
                Def = Math.Max(0, Def - DefRustPerTurn);
                Console.WriteLine(Name + "身上的锈壳又剥落了一层（DEF 降到 " + Def.ToString("F0") + "）。");
            }
            Attack(p);
        }

        /// <summary>受到伤害：走基类骨架（恒等预处理 → DEF 减免 → 护盾）。</summary>
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
