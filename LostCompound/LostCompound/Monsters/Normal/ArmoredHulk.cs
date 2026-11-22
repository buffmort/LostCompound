using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// 装甲（普通怪，6–12 层）—— 拆甲反噬。
    ///
    /// 设计（Hyberty 2026-11-22 设计书）：
    ///   · HP 60、DEF 8 + 开局额外 +15 = **23**、STR 3、ATK 13
    ///   · 每被玩家攻击命中 1 次，DEF −3
    ///   · DEF 扣到 0 时，立刻反击一次，伤害 = ATK × 1.5 ≈ 19
    ///     —— **不吃玩家 DEF、不吃盾**，是机制伤
    ///   · 反击后 DEF 保持 0，不再触发（**每场仅 1 次**）
    ///
    /// 它为什么有意思：DEF 23 意味着前期一刀只能啃掉 1~2 点，玩家会被迫连砍 8 次
    /// （23→20→17→14→11→8→5→2→0）。触底那一刻它一次性把攒下的账还清 ——
    /// 这就是「拆甲」的代价。
    /// </summary>
    public sealed class ArmoredHulk : Monster
    {
        private const double BaseHp = 60;
        private const double BaseDef = 8;
        private const double BaseAtk = 13;

        /// <summary>开局额外护甲：BaseDef + 这一层 = 23。</summary>
        private const double OpeningArmor = 15;

        /// <summary>每被命中一次掉多少 DEF。</summary>
        private const double ArmorLossPerHit = 3;

        /// <summary>反击伤害 = ATK × 这个倍数（设计书 1.5 → 19.5，取整 19）。</summary>
        private const double CounterMultiplier = 1.5;

        /// <summary>这一场是否已经反击过了（每场仅 1 次）。</summary>
        private bool _counterFired;

        public ArmoredHulk()
        {
            Name = "装甲";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = 3;
            Def = BaseDef + OpeningArmor;
            Type = MonsterType.ArmoredHulk;
        }

        public override MonsterRank Rank => MonsterRank.Normal;

        /// <summary>
        /// 意图只有一种：普攻。它没有技能、也不会加盾；战局的变化全在「甲还剩几层」上，
        /// 那个信息在敌人框的 DEF 里，不在意图里 —— 所以意图写死就行。
        /// </summary>
        public override string IntentText() => "打 " + Atk.ToString("F0");

        public override void PlanIntent(Player p) => SetPlannedOption(0);

        public override void Attack(Player p)
        {
            Console.WriteLine("装甲抡起重拳砸了下来！");
            p.TakeDamage(Atk);
        }

        public override void OnMonsterTurn(Player p)
        {
            TakePlannedOption(1);
            Attack(p);
        }

        /// <summary>
        /// 挨打 → 掉甲（反击的判定放在 <see cref="OnPlayerMeleeHit"/> 里，因为那边拿得到 Player）。
        ///
        /// 为什么掉甲在 TakeDamage、反击在钩子里：
        ///   TakeDamage 的签名是 <c>(double dmg)</c>，**没有 Player** ——
        ///   而反击是「对玩家造成伤害」，天生需要玩家对象。改基类签名要动 7 个子类和所有调用点，
        ///   所以走「打完之后由通用流程喊一声」的 <see cref="Monster.OnPlayerMeleeHit"/>，
        ///   与 OnElementalHit / OnSandDug 是同一套做法。
        /// </summary>
        public override void TakeDamage(double dmg)
        {
            TakeDamageWithDefaultLog(dmg);

            if (Hp <= 0) return;      // 已经死了，不用再拆甲

            double before = Def;
            Def = Math.Max(0, Def - ArmorLossPerHit);
            if (Def < before)
            {
                Console.WriteLine("装甲的外壳剥落了一块（DEF " + before.ToString("F0") + " → " + Def.ToString("F0") + "）");
            }
        }

        /// <summary>
        /// 甲碎的那一刻反刺回来 —— 每场只发生一次。
        ///
        /// 为什么不看 hpLoss 是多少：设计书说的是「每被**命中** 1 次 def −3」，
        /// 哪怕这一刀只啃掉 1 点血也算命中，甲照掉。判断「有没有打中」不靠伤害数字，
        /// 而是靠「这一刀是否被盾完全挡住」——被完全挡住时 hpLoss 为 0，
        /// 那种情况连甲皮都没碰到，不拆。
        /// </summary>
        public override void OnPlayerMeleeHit(Player player, double hpLoss)
        {
            if (_counterFired) return;
            if (Hp <= 0) return;
            if (hpLoss <= 0) return;      // 被盾完全挡住，没碰到甲

            if (Def > 0) return;          // 甲还没碎

            _counterFired = true;

            // 设计书写「≈ 19」，而 13 × 1.5 = 19.5：
            // 这里用 Math.Floor 而不是 Math.Round —— Math.Round 默认是**银行家舍入**
            // （.5 向偶数靠拢），19.5 会变成 20，与设计书的 19 对不上。
            // 向下取整也更符合「机制伤别比设计再狠一点」的口径。
            double counter = Math.Floor(Atk * CounterMultiplier);
            Console.WriteLine("装甲的外壳整个碎裂 —— 里面伸出一根骨刺，顺着裂缝狠狠反刺回来！");

            // 机制伤：无视 DEF 与护盾，也**不触发斗篷的「受伤」计数**（设计书要求）。
            // 用 TakeMechanicDamage 而不是 TakeDamage，正是因为它跳过 DEF 减免与 IOnDamaged。
            player.TakeMechanicDamage(counter);
        }

        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);

        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "的装甲把攻击完全弹开了！");
        }

        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
        }
    }
}
