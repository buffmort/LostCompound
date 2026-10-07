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


        // 构造方法：初始化岩浆怪的基础属性
        public LavaSlime()
        {
            Type = MonsterType.LavaSlime;
            Name = "岩浆怪";
            Hp = 15;      // 生命值
            Atk = 5;      // 攻击力
            Str = 1;      // 力量（影响伤害）
            Def = 5;       // 防御（用于玩家攻击岩浆怪时的减伤）
            Block = 0;     // 格挡值
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
                Console.WriteLine(Name + " 点燃了你，造成灼烧效果！");
            }
        }
        /**
         * 怪物受到伤害（直接扣减生命值）
         * @param dmg 伤害值
         */
        public override void TakeDamage(double dmg)
        {
            if (Block > 0)
            {
                Block -= 1;
                Console.WriteLine(Name + "格挡了!");
                return;
            }
            double realDmg = Calculator.ApplyDefense(Def, dmg);
            Hp -= realDmg;
            Console.WriteLine(Name + "受到{0:F1}点伤害!", realDmg);
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
