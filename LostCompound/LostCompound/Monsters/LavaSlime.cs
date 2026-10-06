using LostCompound.Tools;

/**
 * Author:  Hyberty
 * OOP牛逼
 */

namespace LostCompound.Monsters
{
    public class LavaSlime : Monster
    {


        // 构造方法：初始化岩浆怪的基础属性
        public LavaSlime()
        {
            this.monsterType = MonsterType.LAVA_SLIME;
            this.name = "岩浆怪";
            this.hp = 15;      // 生命值
            this.atk = 5;      // 攻击力
            this.str = 1;      // 力量（影响伤害）
            this.def = 5;       // 防御（用于玩家攻击岩浆怪时的减伤）
            this.block = 0;     // 格挡值
            this.DOTdmg = 0;    // 怪物自身的 DOT 伤害（未使用）
            this.DOTdur = 0;    // 怪物自身的 DOT 持续（未使用）
            this.skipTurn = 0;  // 跳过回合数
        }

        /**
         * 岩浆怪的普通攻击，有概率附加灼烧 DOT
         * @param p 玩家对象
         */


        public override void Attack(Player p)
        {
            p.takeDmg(this.atk);

            // 4. 50% 概率附加灼烧 DOT（持续 2 回合，每回合 3 点伤害）
            if (Random.Shared.NextDouble() < 0.5)
            {
                p.setDOTdmg(3);      // 假设 Player 类有类似 Monster 的 DOT 方法
                p.addDOTdur(2);      // 增加 DOT 持续回合
                Console.WriteLine(name + " 点燃了你，造成灼烧效果！");
            }
        }
        /**
         * 怪物受到伤害（直接扣减生命值）
         * @param dmg 伤害值
         */
        public override void takeDmg(double dmg)
        {
            if (this.block > 0)
            {
                block -= 1;
                Console.WriteLine("岩浆史莱姆格挡了!");
                return;
            }
            double realDmg = Calculator.Define(this.def, dmg);
            hp -= realDmg;
            Console.WriteLine(this.name + "受到{0:F1}点伤害!", realDmg);
            Calculator.delay(250);
        }

        /**
         * 怪物回合行为：直接执行攻击
         * @param p 玩家对象
         */
        public override void onMonsterTurn(Player p)
        {
            Console.WriteLine("=====" + this.name + "回合=====");
            Attack(p);
        }

        /**
         * 随机选择索引（用于多技能选择，此处简单返回 0~i-1 的随机数）
         * @param i 选项总数
         * @return 随机索引
         */
        protected override int rdChoice(int i)
        {
            return (int)(Random.Shared.NextDouble() * i);
        }

        /**
         * 判断怪物是否存活
         * @return true=存活，false=死亡
         */
        public override bool isAlive()
        {
            return hp > 0;
        }
    }
}
