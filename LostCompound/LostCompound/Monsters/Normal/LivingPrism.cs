using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// 活棱镜（普通怪，6–12 层）—— 伤害反弹。
    ///
    /// 设计（Hyberty 2026-11-22 设计书）：
    ///   · HP 55、DEF 3、STR 3、ATK 11
    ///   · 每次被玩家攻击命中，反弹**实际伤害的 30%（向下取整）**给玩家
    ///   · 反弹**不吃玩家 DEF、不吃盾**，直接扣血（真伤）
    ///   · 反弹**不触发玩家的「受伤」判定**（斗篷不计数）
    ///
    /// 它的价值不在活着，在「你敢不敢打」：玩家打 200 → 自己吃 60；
    /// 琴弦全开打 1800 → 自己吃 540，比 Boss 还狠。
    /// 设计书注明「DOT 不反弹」—— 所以流血/磷灼烧这类每回合结算的伤害走的是别的通路，
    /// 不经过 <see cref="OnPlayerMeleeHit"/>，天然不会被反弹。
    /// </summary>
    public sealed class LivingPrism : Monster
    {
        private const double BaseHp = 55;
        private const double BaseDef = 3;
        private const double BaseAtk = 11;

        /// <summary>反弹比例：实际伤害的 30%，向下取整。</summary>
        private const double ReflectRatio = 0.30;

        public LivingPrism()
        {
            Name = "活棱镜";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = 3;
            Def = BaseDef;
            Type = MonsterType.LivingPrism;
        }

        public override MonsterRank Rank => MonsterRank.Normal;

        public override string IntentText() => "打 " + Atk.ToString("F0");

        public override void PlanIntent(Player p) => SetPlannedOption(0);

        public override void Attack(Player p)
        {
            Console.WriteLine("活棱镜折射出一道刺目的光，直射过来！");
            p.TakeDamage(Atk);
        }

        public override void OnMonsterTurn(Player p)
        {
            TakePlannedOption(1);
            Attack(p);
        }

        /// <summary>
        /// 挨打。这里只负责掉血与播报，**反弹放在 <see cref="OnPlayerMeleeHit"/> 里**：
        /// 反弹要对玩家造成伤害，而 TakeDamage 的签名拿不到 Player。
        ///
        /// 用带 out 的版本是为了顺手把「实际扣掉多少血」算出来 ——
        /// 反弹按**实际伤害**算，被 DEF/盾挡掉的部分不反弹（设计书写的是「实际伤害」）。
        /// </summary>
        public override void TakeDamage(double dmg)
        {
            TakeDamageWithDefaultLog(dmg);
        }

        /// <summary>
        /// 按这一刀**实际扣掉的血**反弹 30%（向下取整）给玩家。
        ///
        /// 三个「不吃」都靠 <see cref="Player.TakeMechanicDamage"/> 保证：
        /// 它跳过 DEF 减免、跳过护盾、也跳过 IOnDamaged（斗篷不计数）——
        /// 正好对应设计书的「真伤、不触发受伤判定」。
        ///
        /// 为什么按 hpLoss 而不是打过来的原始伤害：护盾吃掉的部分没打到棱镜身上，
        /// 没产生反射，不该反弹。
        /// </summary>
        public override void OnPlayerMeleeHit(Player player, double hpLoss)
        {
            if (hpLoss <= 0) return;      // 被盾完全挡住 = 没打中棱镜，无反可弹

            double reflect = Math.Floor(hpLoss * ReflectRatio);
            if (reflect <= 0) return;     // 向下取整后不足 1 点就不弹（设计书「向下取整」）

            Console.WriteLine("活棱镜把那道伤害折了回来！（反弹 " + reflect.ToString("F0") + "）");
            player.TakeMechanicDamage(reflect);
        }

        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);

        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "的表面泛起一层硬光，把攻击滑开了！");
        }

        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
        }
    }
}
