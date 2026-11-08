using LostCompound.Buffs.Types;
using LostCompound.Monsters;
using LostCompound.Rooms;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// **硫磺怪**（设计文档 §6「地图 1 · 熔岩瀑布」专属怪，Hyberty 2026-11-08 定）。
    ///
    /// 数值：`hp 20` / `def 2` / `atk 5` / `str 0`，档位 **普通**。
    /// 行为：**必定普通攻击**（没有技能、不掷骰子选招式）。
    ///
    /// 它的特色全押在「站在高温房间里」这一条上：
    ///   · **普通房间**：就是一次普通的 5 点攻击，正常过玩家的 DEF 与护盾。
    ///   · **高温房间**（<see cref="Room.IsHot"/>）：
    ///       ① 攻击**无视 DEF 与护盾**，5 点直接砍血（见
    ///          <see cref="Player.TakeDamageIgnoringDefenseAndShield"/>）；
    ///       ② 每次攻击附带 **1 层 / 1 回合的流血**。
    ///
    /// 为什么流血也只在高温房间挂（Hyberty 裁定）：这只怪的设定是「硫磺在高温下才烫人」，
    /// 普通房间里它只是个普通的 20 血小怪 —— 玩家因此有理由在高温房前先想清楚要不要打。
    ///
    /// 怎么知道「是不是高温房间」：读玩家当前所在房间的 <see cref="Room.IsHot"/>。
    /// 战斗中玩家就站在那间战斗房里，所以这个标记与「这场战斗在哪打的」是同一件事；
    /// 环境对象是整层共享的、查不到具体房间，所以只能从玩家这一侧问。
    /// </summary>
    public sealed class SulphurMonster : Monster
    {
        /// <summary>档位：普通（与岩浆怪同档）。</summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        /// <summary>攻击力（设计文档 §6：5）。</summary>
        public const double BaseAtk = 5;

        /// <summary>力量（裁定：0 —— 它不吃力量加成，伤害就是固定的 5）。</summary>
        public const double BaseStr = 0;

        /// <summary>生命（裁定：20）。</summary>
        public const double BaseHp = 20;

        /// <summary>防御（裁定：2，用于玩家打它时的减伤）。</summary>
        public const double BaseDef = 2;

        /// <summary>每次攻击挂的流血层数（裁定：1 层 / 1 回合）。</summary>
        public const int BleedStacksPerHit = 1;

        /// <summary>每次攻击挂的流血持续回合（裁定：1 回合）。</summary>
        public const int BleedTurnsPerHit = 1;

        public SulphurMonster()
        {
            Type = MonsterType.SulphurMonster;
            Name = "硫磺怪";
            Hp = BaseHp;
            Atk = BaseAtk;
            Str = BaseStr;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        /// <summary>
        /// 普通攻击（必定走这条，没有别的招式）。
        ///
        /// 高温房间里换成「无视 DEF 与盾」的伤害入口，并额外挂流血；
        /// 判定放在方法内而不是开战时缓存，是因为**战斗期间房间不会变**，
        /// 每次读一下既不会错、也省掉一个「什么时候刷新这个字段」的时序问题。
        /// </summary>
        public override void Attack(Player p)
        {
            bool inHotRoom = p.CurrentRoom?.IsHot == true;

            if (inHotRoom)
            {
                Console.WriteLine(Name + "在高温里烧得通红，攻击无视了防御与护盾！");
                p.TakeDamageIgnoringDefenseAndShield(Atk);
                p.AddBuff(new BleedBuff(BleedStacksPerHit, BleedTurnsPerHit));
                Console.WriteLine(Name + "的硫磺烟气灼伤了你，你开始流血！");
                return;
            }

            p.TakeDamage(Atk);
        }

        /// <summary>怪物回合：必定普通攻击（跳过检查由 Tools.RunBattle 统一处理）。</summary>
        public override void OnMonsterTurn(Player p)
        {
            Attack(p);
        }

        /// <summary>
        /// 受到伤害：走基类骨架（默认恒等预处理 → DEF 减免 → 护盾）。
        /// 硫磺怪没有特殊的抗性，所以只需要提供两句播报文案。
        /// </summary>
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

        /// <summary>
        /// 本怪不用多技能，但基类要求实现（<see cref="Monster.RollIndex"/> 是 abstract）。
        /// 按「返回 0..count-1」的约定实现，与其它普通怪一致。
        /// </summary>
        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);
    }
}
