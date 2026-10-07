using LostCompound.Tools;

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
            DotDamage = 0;    // 怪物自身的 DOT 伤害（未使用）
            DotDuration = 0;    // 怪物自身的 DOT 持续（未使用）
            SkipTurn = 0;  // 跳过回合数
        }

        /**
         * 岩浆怪的普通攻击，有概率附加灼烧 DOT
         * @param p 玩家对象
         */


        public override void Attack(Player p)
        {
            p.TakeDamage(Atk);

            // 4. 50% 概率附加灼烧 DOT（持续 2 回合，每回合 3 点伤害）
            if (Random.Shared.NextDouble() < 0.5)
            {
                p.DotDamage = 3;      // 注意这里是直接赋值：再次点燃会覆盖伤害而不是叠加
                p.AddDotDuration(2);      // 持续回合则是累加
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
                Console.WriteLine("岩浆史莱姆格挡了!");
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
            Console.WriteLine("=====" + Name + "回合=====");
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
