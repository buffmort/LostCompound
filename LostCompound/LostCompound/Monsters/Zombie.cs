using LostCompound.Tools;

namespace LostCompound.Monsters
{
    public sealed class Zombie : Monster
    {
        public Zombie()
        {
            Atk = 20;
            Str = 0.5;
            Def = 0;
            Hp = 30;
            Name = "僵尸";
        }
        public Zombie(double atk, double str, double def, double hp, string name)
        {
            Atk = atk;
            Def = def;
            Str = str;
            Hp = hp;
            Name = name;
        }

        public override void Attack(Player p)
        {
            Console.WriteLine("僵尸咬了你一口!");
            Calculator.Delay(500);
            double dmg = Calculator.CalculateAttack(Str, Atk);
            p.TakeDamage(dmg);

        }

        public override void TakeDamage(double dmg)
        {
            if (Block > 0)
            {
                Block -= 1;
                Console.WriteLine("僵尸格挡了伤害!");
                return;
            }
            double realDmg = Calculator.ApplyDefense(Def, dmg);
            Hp -= realDmg;
            Console.WriteLine(Name + "受到{0:F1}点伤害!", realDmg);
            Calculator.Delay(250);
        }

        public override void OnMonsterTurn(Player p)
        {
            if (DotDuration > 0)
            {
                DotDuration -= 1;
                Hp -= DotDamage;
            }
            Console.WriteLine("=====怪物" + Name + "回合=====");
            if (SkipTurn > 0)
            {
                SkipTurn -= 1;
                Console.WriteLine("跳过回合!");
                return;
            }
            Calculator.Delay(250);
            int option = RollIndex(2);
            switch (option)
            {
                case 0:
                    Attack(p);
                    break;
                case 1:
                    Block += 1;
                    Console.WriteLine(Name + "格挡了!");
                    break;
            }
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count);
    }
}
