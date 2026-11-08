using LostCompound.Tools;
using LostCompound.Monsters.Capabilities;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// 食人鱼（DESIGN §6「地图 2 水淹洞穴」，普通怪）。
    ///
    /// 数值与机制（文档原文）：「10 血；无法被攻击，可被 AOE 打；
    ///   每回合攻击 5（困难 10）；怪物全死则自动死亡」。
    ///
    /// 三条实现口径：
    ///   ① **不能被单体选中** —— 实现 <see cref="ICannotBeTargeted"/>，
    ///      `Player.Attack` 的目标列表会把它排除。全体 AOE 不受影响（Hyberty裁定）。
    ///   ② **每回合攻击 5** 是**固定伤害**，不是走 `Calculator.CalculateAttack` 的推导值：
    ///      文档给的就是「攻击 5」这一个数，硬套公式（STR 0 时算出 2）反而对不上。
    ///      走 <see cref="Player.TakeDamage"/> 的好处是：照常过 DEF 减免与护盾，
    ///      与其它怪的伤害走同一条管线。
    ///   ③ **怪物全死则自动死亡**：它的回合里若没有其它活着的同伴，自己归零。
    ///      为什么放在它自己的回合而不是战斗循环里：只有这里能同时看到
    ///      `BattleAllies`（同一场战斗的怪物列表）与自己的状态，且时机自然
    ///      （同伴死光了，轮到它时才发现自己该消失）。归零后由
    ///      `Tools.RemoveDeadMonsters` 统一收尸，不会漏。
    /// </summary>
    public sealed class Piranha : Monster, ICannotBeTargeted
    {
        /// <summary>每回合造成的固定伤害（文档：5；困难 10 待难度系统）。</summary>
        public const double BiteDamage = 5;

        /// <summary>血量（文档：10）。</summary>
        public const double MaxHp = 10;

        public Piranha()
        {
            Type = MonsterType.Piranha;
            Name = "食人鱼";
            Hp = MaxHp;
            Atk = 0;      // 伤害是固定值，不走攻击公式（见类注释②）
            Str = 0;
            Def = 0;
            Shield = 0;
            SkipTurn = 0;
        }

        public override MonsterRank Rank => MonsterRank.Normal;

        public override void Attack(Player p)
        {
            Console.WriteLine(Name + "咬了你一口!");
            p.TakeDamage(BiteDamage);
        }

        public override void TakeDamage(double dmg)
        {
            TakeDamageWithDefaultLog(dmg);
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count);

        /// <summary>
        /// 每回合：先检查「同伴是否全死」，再咬一口。
        /// 跳过回合的检查与其它怪同一套（被热硫/压缩沙瓶跳过时不该还能咬人）。
        /// </summary>
        public override void OnMonsterTurn(Player p)
        {
            if (!HasLivingAlly())
            {
                Console.WriteLine(Name + "失去了同伴，翻起了肚皮。");
                Hp = 0;
                return;
            }

            // 跳过检查**不在这里做**：战斗循环（Tools.RunBattle）在调用 OnMonsterTurn 之前
            // 已经统一调过一次 m.TryConsumeSkipTurn() 并 continue 掉了。
            // 这里再调一次会把「跳过 1 回合」扣成 2 点库存 —— 热硫的 1 回合、压缩沙瓶的 2 回合
            // 都会因此缩水一半（探针 probe_skip 抓到过）。

            Attack(p);
        }

        /// <summary>场上还有没有**别的**活着的怪（不含自己）。</summary>
        private bool HasLivingAlly()
        {
            foreach (Monster ally in BattleAllies)
            {
                if (ReferenceEquals(ally, this)) continue;
                if (ally.IsAlive) return true;
            }
            return false;
        }
    }
}
