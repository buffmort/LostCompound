using LostCompound.Tools;

namespace LostCompound.Monsters
{
    public class Zombie : Monster
    {
        public Zombie()
        {
            this.atk = 20;
            this.str = 0.5;
            this.def = 0;
            this.hp = 30;
            this.name = "僵尸";
        }
        public Zombie(double atk, double str, double def, double hp, string name)
        {
            this.atk = atk;
            this.def = def;
            this.str = str;
            this.hp = hp;
            this.name = name;
        }
        public Random rd = new Random();
        public override void Attack(Player p)
        {
            Console.WriteLine("僵尸咬了你一口!");
            Calculator.delay(500);
            double dmg = Calculator.Attack(this.str, this.atk);
            p.takeDmg(dmg);

        }

        public override void takeDmg(double dmg)
        {
            if (this.block > 0)
            {
                block -= 1;
                Console.WriteLine("僵尸格挡了伤害!");
                return;
            }
            double realDmg = Calculator.Define(this.def, dmg);
            hp -= realDmg;
            Console.WriteLine(this.name + "受到{0:F1}点伤害!", realDmg);
            Calculator.delay(250);
        }

        public override void onMonsterTurn(Player p)
        {
            if (this.DOTdur > 0)
            {
                this.DOTdur -= 1;
                this.hp -= DOTdmg;
            }
            Console.WriteLine("=====怪物" + this.name + "回合=====");
            if (skipTurn > 0)
            {
                skipTurn -= 1;
                Console.WriteLine("跳过回合!");
                return;
            }
            Calculator.delay(250);
            int option = rdChoice(2);
            switch (option)
            {
                case 0:
                    Attack(p);
                    break;
                case 1:
                    block += 1;
                    Console.WriteLine(this.name + "格挡了!");
                    break;
            }
        }

        protected override int rdChoice(int i)
        {
            return rd.Next(i);
        }
        public override bool isAlive()
        {
            return this.hp > 0;
        }
    }
}
