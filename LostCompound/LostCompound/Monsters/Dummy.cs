using LostCompound.Tools;

namespace LostCompound.Monsters
{
    public class Dummy : Monster
    {
        public Dummy()
        {
            this.monsterType = MonsterType.DUMMY;
            this.name = "测试假人";
            this.hp = 1000;
            this.block = 0;
        }
        public override void Attack(Player p)
        {
            Console.WriteLine("假人无动于衷...");
        }

        public override void takeDmg(double dmg)
        {
            double realDmg = Calculator.Define(this.def, dmg);
            hp -= realDmg;
            Console.WriteLine(this.name + "受到{0:F1}点伤害!", realDmg);
            Calculator.delay(250);
        }

        public override void onMonsterTurn(Player p)
        {
            Console.WriteLine("=====怪物" + this.name + "回合=====");
            if (skipTurn > 0)
            {
                skipTurn -= 1;
                Console.WriteLine("跳过回合!");
                return;
            }
            Attack(p);
        }

        protected override int rdChoice(int i)
        {
            return 0;
        }

        public override bool isAlive()
        {
            return hp > 0;
        }
    }
}
