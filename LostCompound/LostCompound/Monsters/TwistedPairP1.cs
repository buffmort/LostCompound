using LostCompound.Tools;

/**
 * Author:  Hyberty
 * OOP牛逼
 */

/**
 * 作孽双子 - p1（3技能）
 */
namespace LostCompound.Monsters
{
    public class TwistedPairP1 : Monster
    {
        private readonly TwistedPairState state;
        public Random rd = new Random();

        public TwistedPairP1(TwistedPairState state)
        {
            this.monsterType = MonsterType.TWISTEDPAIR1;
            this.name = "Twisted Pair P1";
            this.hp = 30;
            this.def = 3;
            this.state = state;
            // 其他属性默认即可
        }

        public override void Attack(Player p)
        {
            // 普通攻击（若需要）
            p.takeDmg(10);
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
            state.checkAndResetIfBothTrue();  // 新回合检测


            int choice = rdChoice(3); // 3个技能
            switch (choice)
            {
                case 1:
                    Console.WriteLine(this.name + "攻击了你!");
                    state.p1AttackedThisTurn = true;// 标记p1已行动
                    p.takeDmg(10);
                    break;
                case 2:
                    Console.WriteLine(this.name + "向你喷了火!");
                    Console.WriteLine("你受到持续5回合的3点DOT伤害!");
                    p.addDOTdmg(3);
                    p.addDOTdur(5);
                    break;
                case 3:
                    Console.WriteLine(this.name + "困住了你！");
                    Console.WriteLine("你下一回合无法行动!");
                    p.setSkipTurn(1);
                    break;
            }
        }

        protected override int rdChoice(int i)
        {
            return rd.Next(i) + 1;
        }

        public override bool isAlive()
        {
            return hp > 0;
        }
    }
}
