using LostCompound.Tools;

/**
 * Author:  Hyberty
 * OOP牛逼
 */

/**
 * 作孽双子 - p2（4技能）
 */
namespace LostCompound.Monsters
{
    public class TwistedPairP2 : Monster
    {
        private readonly TwistedPairState state;
        private readonly TwistedPairP1 brother;
        public Random rd = new Random();

        public TwistedPairP2(TwistedPairState state, TwistedPairP1 brother)
        {
            this.name = "Twisted Pair P2";
            this.monsterType = MonsterType.TWISTEDPAIR2;
            this.hp = 30;
            this.def = 5;
            this.state = state;
            this.brother = brother;
        }

        public override void Attack(Player p)
        {
            p.takeDmg(5);
        }

        public override void takeDmg(double dmg)
        {
            if (block > 0)
            {
                block--;
                return;
            }
            hp -= dmg;
        }

        public override void onMonsterTurn(Player p)
        {
            Console.WriteLine("=====" + this.name + "回合=====");
            state.checkAndResetIfBothTrue();
            state.p2AttackedThisTurn = true;  // 标记p2已行动

            // 第一次行动（允许触发额外行动）
            bool extraNeeded = performAction(p, true);
            // 如果技能④触发额外行动，则再执行一次（不允许再次额外）
            if (extraNeeded)
            {
                performAction(p, false);
            }
        }

        /**
         * 执行一次随机技能。
         * @param allowExtra 是否允许技能④触发额外行动
         * @return 是否触发了额外行动（用于外层判断）
         */
        private bool performAction(Player p, bool allowExtra)
        {
            int choice = rdChoice(4); // 4个技能
            switch (choice)
            {
                case 1:
                    Console.WriteLine(this.name + "攻击了你!");
                    p.takeDmg(5);
                    break;
                case 2:
                    // 技能2：造成1点伤害5次
                    Console.WriteLine(this.name + "攻击了你5次!");
                    for (int i = 0; i < 5; i++)
                    {
                        p.takeDmg(1);
                    }
                    break;
                case 3:
                    // 技能3：给所有怪物加1格挡，若任意怪物已有格挡则跳过本回合
                    if (this.block > 0 || brother.block > 0)
                    {
                        return false; // 直接不行动
                    }
                    this.block++; // 给自己加格挡
                    brother.block++; // 给 p1 加格挡
                    break;
                case 4:
                    // 技能4：造成5伤害，若p1未攻击则额外行动一次
                    p.takeDmg(5);
                    if (allowExtra && !state.p1AttackedThisTurn)
                    {
                        Console.WriteLine("因为p1未行动,所以" + this.name + "再行动一次!");
                        return true; // 触发额外行动
                    }
                    break;
            }
            return false;
        }

        protected override int rdChoice(int i)
        {
            return rd.Next(i) + 1; // 返回 1 ~ i
        }

        public override bool isAlive()
        {
            return hp > 0;
        }
    }
}
