using LostCompound.Tools;
using LostCompound.Monsters;

using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// 骷髅（设计文档 §6.2「普通怪物（全地图可出现）」）：
    /// <c>hp 40</c>、<c>def 1</c>、<c>str 2</c>、<c>atk 10</c>。
    ///
    /// 文档只给了这四组数值，**没有给技能表**，所以这里按用户的裁定做成
    /// 「只有普通攻击」的怪：伤害走 <see cref="Calculator.CalculateAttack(double, double)"/>，
    /// 与僵尸同一套公式（`2 + STR × √ATK`），不自行发明任何技能 / 概率效果。
    /// </summary>
    public sealed class Skeleton : Monster
    {
        /// <summary>
        /// 档位：**普通**。依据：设计文档 §6.2 把它列在「普通怪物（全地图可出现）」里。
        /// （Boss 在 §6.1，精英在 §6 表；骷髅都不属于那两处，故不是精英 / Boss。）
        /// </summary>
        public override MonsterRank Rank => MonsterRank.Normal;

        public Skeleton()
        {
            // ⚠️ Type 必须显式赋值：Monster.Die() 用 Type 去查 MonsterLootTable，
            // 漏设会走 default 分支「你获得了一个大大的空集!」—— 编译、运行都不报错，
            // 只是骷髅静默不掉落。这是项目里踩过的坑（见 Monster.Type 的注释）。
            Type = MonsterType.Skeleton;

            Name = "骷髅";
            Hp = 40;   // 生命值（设计文档 §6.2）
            Atk = 10;  // 攻击力（设计文档 §6.2）
            Str = 2;   // 力量（设计文档 §6.2，参与攻击力公式）
            Def = 1;   // 防御（设计文档 §6.2）
        }

        /// <summary>
        /// 普通攻击：按「力量 × 攻击力」公式算伤害，再交给玩家结算。
        /// 与 Zombie.Attack 用的是同一个公式与同一条受伤入口，不额外附加流血 / 护盾等效果。
        /// </summary>
        public override void Attack(Player p)
        {
            Console.WriteLine("骷髅挥动骨爪攻击了你! " +
                              "它的爪子更锋利了!");
            double dmg = Calculator.CalculateAttack(Str, Atk);
            Atk += 2;
            p.TakeDamage(dmg);
        }

        public override void TakeDamage(double dmg)
        {
            // 顺序：先 DEF 减免，再吃盾 —— 骨架在 Monster.TakeDamageWithDefaultLog 里。
            // 本类只提供自己那两句文案，不覆写预处理（骷髅没有元素抗性 / 伤害免疫）。
            TakeDamageWithDefaultLog(dmg);
        }

        /// <summary>护盾吃光整刀时的播报（措辞与其它普通怪一致）。</summary>
        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "的护盾挡下了伤害！（剩余护盾:" + Shield.ToString("F1") + "）");
        }

        /// <summary>实际扣血时的播报（措辞与其它普通怪一致）。</summary>
        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
        }

        /// <summary>
        /// 怪物回合：骷髅只有普通攻击这一种行动，所以直接打，不做随机分支。
        /// 回合头、流血结算与跳过检查都由 Tools.RunBattle 统一处理，这里不重复。
        /// </summary>
        public override void OnMonsterTurn(Player p)
        {
            Attack(p);
        }

        /// <summary>
        /// 基类要求实现的随机索引入口。骷髅目前没有多技能可选，
        /// 保留标准实现只是为了满足抽象契约（将来若加技能可直接用）。
        /// </summary>
        protected override int RollIndex(int count) => Random.Shared.Next(count);
    }
}
