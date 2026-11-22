using LostCompound.Buffs;
using LostCompound.Buffs.Types;
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Elite
{
    /// <summary>
    /// **放射僵尸**（Hyberty 2026-11-22 设计，放射性区域专属精英）。
    ///
    /// 数值：`hp 90` / `def 5` / `atk 10 + 玩家辐照层数 × 5`，档位 **精英**。
    ///
    /// 机制（互相咬合，形成「越拖越疼」的正反馈）：
    ///   · 每次攻击**命中**给玩家 +1 层 `辐照`（上限 3 层，战斗结束自动清掉）；
    ///   · 玩家身上每 1 层，它自己的 ATK **+5**。
    ///   所以第 1 刀 10、第 2 刀 15、第 3 刀 20、之后稳定 25 ——
    ///   玩家拖得越久越吃亏，「先杀它」是唯一解，与秒针虫构成同一张图上的两道优先级题。
    ///
    /// ⚠️ 攻击力是**每次现算**（`Atk + 层数 × 5`），不是把 `Atk` 字段改掉：
    ///   改字段的话「层数掉了」时还得记得改回来，忘一次就永久变强 —— 那是静默错值。
    ///
    /// ⚠️ 「命中」的含义与其它怪一致：**真的打出去了**就算命中，
    ///   被 DEF/护盾挡下也算（见 §7.1 的口径），只有「玩家闪避/不存在」才不算。
    ///   这一层是 buff，不是伤害，所以挡不挡都叠 —— 否则玩家叠 DEF 就能把机制整个关掉。
    /// </summary>
    public sealed class RadioactiveZombie : Monster
    {
        /// <summary>档位：精英。</summary>
        public override MonsterRank Rank => MonsterRank.Elite;

        /// <summary>血量（Hyberty：90）。</summary>
        public const double BaseHp = 90;

        /// <summary>基础攻击力（Hyberty：10）。</summary>
        public const double BaseAtk = 10;

        public const double BaseDef = 5;
        public const double BaseStr = 0;

        /// <summary>玩家每有 1 层辐照，它的 ATK 加这么多（Hyberty：5）。</summary>
        public const double AtkPerRadiationStack = 5;

        /// <summary>每次命中给玩家叠几层（Hyberty：1）。</summary>
        public const int StacksPerHit = 1;

        /// <summary>`辐照` 的持续时间。战斗结束会整个清掉，所以给一个够长的窗口即可。</summary>
        private const int RadiationStackTurns = 9;

        public RadioactiveZombie()
        {
            Type = MonsterType.RadioactiveZombie;
            Name = "放射僵尸";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = BaseStr;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        /// <summary>玩家身上当前有几层辐照（读的是 buff，不是本地计数器）。</summary>
        public static int RadiationStacksOf(Player p)
        {
            if (p == null) return 0;
            foreach (Buff b in p.Buffs)
            {
                if (b is RadiationStack rs) return rs.Stacks;
            }
            return 0;
        }

        /// <summary>本回合实际打出的攻击力 = 基础 ATK + 辐照层数 × 5，每次现算。</summary>
        public double CurrentAttack(Player p) => Atk + RadiationStacksOf(p) * AtkPerRadiationStack;

        public override void Attack(Player p)
        {
            double dmg = CurrentAttack(p);

            Console.WriteLine(Name + "喷出一股辐射尘，扑了过来！");
            p.TakeDamage(dmg);

            if (!p.IsAlive) return;

            // 命中就叠：**在伤害结算之后**叠，
            // 这样「这一刀」用的是它挨打之前的层数，玩家看到的数字和意图能对上。
            GiveRadiationStack(p);
        }

        /// <summary>给玩家叠一层辐照（封顶 3，由 `RadiationStack.MaxStacks` 管）。</summary>
        private void GiveRadiationStack(Player p)
        {
            foreach (Buff b in p.Buffs)
            {
                if (b is RadiationStack exist)
                {
                    int before = exist.Stacks;
                    exist.AddStacks(StacksPerHit);
                    exist.AddTurns(RadiationStackTurns - exist.RemainingTurns);
                    if (exist.Stacks > before)
                    {
                        Console.WriteLine(Name + "的辐射渗进你的身体（辐照 " + exist.Stacks + "/" +
                                          RadiationStack.MaxStackCount + "，它的攻击更狠了）");
                    }
                    return;
                }
            }

            var fresh = new RadiationStack(StacksPerHit, RadiationStackTurns);
            p.AddBuff(fresh);
            Console.WriteLine(Name + "的辐射渗进你的身体（辐照 " + fresh.Stacks + "/" +
                              RadiationStack.MaxStackCount + "，它的攻击更狠了）");
        }

        /// <summary>
        /// 意图：写**实际会打出的伤害**，不是基础 10 —— 玩家要能看见「拖下去它在变强」。
        /// 与伤害后叠加的顺序一致：这一回合打出去的是「当前层数」算出来的值。
        /// </summary>
        public override string IntentText(Player p)
        {
            return "打 " + CurrentAttack(p).ToString("F0");
        }

        public override string IntentText() => "打 " + Atk.ToString("F0");

        public override void OnMonsterTurn(Player p)
        {
            Attack(p);
        }

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
