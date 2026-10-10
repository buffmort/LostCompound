using LostCompound.Tools;
using LostCompound.Buffs;

/**
 * Author:  Hyberty
 * OOP牛逼
 */

namespace LostCompound.Monsters
{
    public sealed class LavaSlime : Monster
    {
        /// <summary>熔岩瀑布（高温环境）里岩浆怪的高温伤害免疫比例（设计文档六：免疫 30%）。</summary>
        public const double HighTempImmunityInLavaWaterfall = 0.30;

        /// <summary>水瓶对岩浆怪造成的「当前血量」扣除比例（设计文档六：当前 70%）。</summary>
        public const double WaterBottleHpLossRatio = 0.70;

        /// <summary>
        /// 高温伤害免疫比例（0~1），由战斗开始时按环境写入（见 Tools.ApplyEnvironmentMonsterTraits）。
        ///
        /// 为什么做成「开战时写入的字段」而不是在 TakeDamage 里读环境：
        /// Monster.TakeDamage(double dmg) 只有伤害一个参数，拿不到环境；给它加参数会牵动
        /// 基类、全部怪物子类和所有调用点（含 BleedBuff），改动面远大于收益。
        /// 所以把「环境 → 抗性」这一步放在战斗开始处一次性结算，怪物自身只认比例。
        /// 默认 0 = 无免疫（非熔岩瀑布环境，或该特性未初始化时）。
        /// </summary>
        private double _highTempImmunity;

        /// <summary>战斗开始时按环境写入高温免疫比例（0~1）。</summary>
        public void SetHighTempImmunity(double ratio) => _highTempImmunity = ratio;

        // 构造方法：初始化岩浆怪的基础属性
        public LavaSlime()
        {
            Type = MonsterType.LavaSlime;
            Name = "岩浆怪";
            Hp = 80;      // 生命值（设计文档六：80；此前误写为 15）
            Atk = 5;      // 攻击力
            Str = 1;      // 力量（影响伤害）
            Def = 5;       // 防御（用于玩家攻击岩浆怪时的减伤）
            Shield = 0;    // 护盾值（原 Block = 0 的次数制已删除；这里显式写 0 只是保留原行意图）
            SkipTurn = 0;  // 跳过回合数
        }

        /**
         * 岩浆怪的普通攻击，有概率附加灼烧（走流血 buff）
         * @param p 玩家对象
         */


        public override void Attack(Player p)
        {
            p.TakeDamage(Atk);

            // 4. 50% 概率附加灼烧（持续 2 回合，每回合 3 点伤害）
            // 原先写法是「p.DotDamage = 3（直接赋值，再次点燃覆盖伤害）+ AddDotDuration(2)（累加）」；
            // DOT 机制已删除，改为挂等值的流血。注意叠加口径随之统一为流血的
            // 「伤害 min(现有+3, 10)、剩余回合相加」—— 多次点燃现在会比原来更疼（原来伤害恒定 3）。
            if (Random.Shared.NextDouble() < 0.5)
            {
                p.AddBuff(new BleedBuff(3, 2));
                Console.WriteLine(Name + " 点燃了你，造成流血效果！");
            }
        }
        /**
         * 怪物受到伤害（直接扣减生命值）
         * @param dmg 伤害值
         */
        public override void TakeDamage(double dmg)
        {
            // 高温免疫（设计文档六：免疫 30% 高温伤害）。
            // 先按比例削掉「造成的伤害」，再交给 DEF 减免 —— 文档说的是「造成的伤害减少 30%」，
            // 作用对象是伤害本身，而不是减免后的结果。
            double afterImmunity = dmg * (1.0 - _highTempImmunity);
            // 顺序：高温抗性 → DEF → 护盾。三者的作用对象都是「这一击的伤害」，
            // 从外到内依次打折，最后剩下的部分才动血条（盾是最内层，因为它就是额外血量）。
            double realDmg = Calculator.ApplyDefense(Def, afterImmunity);
            double hpLoss = AbsorbWithShield(realDmg);
            if (hpLoss <= 0)
            {
                Console.WriteLine(Name + "的护盾挡下了伤害！（剩余护盾:" + Shield.ToString("F1") + "）");
                Calculator.Delay(250);
                return;
            }
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
            Calculator.Delay(250);
        }

        /**
         * 怪物回合行为：直接执行攻击
         * @param p 玩家对象
         */
        public override void OnMonsterTurn(Player p)
        {
            // 回合头与跳过检查都集中在 Tools.RunBattle：
            // 原先这里没有跳过检查（漏写），热硫的 AddSkipTurn 对岩浆怪无效 ——
            // 现在统一走循环里的 m.TryConsumeSkipTurn() 后就自动修好了。
            Attack(p);
        }

        /**
         * 随机选择索引（用于多技能选择，此处简单返回 0~i-1 的随机数）
         * @param i 选项总数
         * @return 随机索引
         */
        protected override int RollIndex(int count) => (int)(Random.Shared.NextDouble() * count);
    }
}
