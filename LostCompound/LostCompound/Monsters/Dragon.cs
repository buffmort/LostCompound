using LostCompound.Tools;

namespace LostCompound.Monsters
{
    public class Dragon : Monster
    {
        private const int MAX_BLOCK = 1;    // 格挡上限
        private const double MIN_DMG = 1.0; // 每次受伤最低值

        public Dragon()
        {
            // 龙的基础属性：防御力 5，生命值高一些，攻击力等可以按需调整
            this.monsterType = MonsterType.DRAGON;

            this.atk = 10;      // 普通攻击力（可能不会被使用，因为我们会直接固定伤害）
            this.str = 0.0;     // 力量修正（若使用 Calculator.Attack 时需要）
            this.def = 5;       // 防御力 5
            this.hp = 80;       // 龙的生命值
            this.name = "龙";
        }

        // 可选的带参数构造器，方便扩展
        public Dragon(double atk, double str, double def, double hp, string name)
        {
            this.atk = atk;
            this.str = str;
            this.def = def;
            this.hp = hp;
            this.name = name;
        }

        private readonly Random rd = new Random();

        public override void Attack(Player p)
        {
            // 默认普通攻击：造成 10 点伤害（对应第二种行动）
            Console.WriteLine(this.name + "喷出烈焰！");
            Calculator.delay(500);
            p.takeDmg(10);
        }

        public override void takeDmg(double dmg)
        {
            // 如果拥有格挡，消耗一次格挡并完全抵消本次伤害
            if (this.block > 0)
            {
                this.block--;
                Console.WriteLine(this.name + "用鳞甲格挡了攻击！");
                return;
            }

            // 计算减伤后的伤害
            double realDmg = Calculator.Define(this.def, dmg);
            // 龙族特性：受到的伤害至少为 1（除非被格挡）
            if (realDmg < MIN_DMG)
            {
                realDmg = MIN_DMG;
            }

            this.hp -= realDmg;
            Console.WriteLine("{0}受到 {1:F1} 点伤害！", this.name, realDmg);
            Calculator.delay(250);
        }

        public override void onMonsterTurn(Player p)
        {
            Console.WriteLine("=====" + this.name + "回合=====");
            Console.WriteLine("=====怪物 " + this.name + " 回合=====");
            if (this.skipTurn > 0)
            {
                this.skipTurn--;
                Console.WriteLine(this.name + "被眩晕，跳过回合！");
                return;
            }

            Calculator.delay(250);
            int option = rdChoice(3); // 随机 0,1,2

            switch (option)
            {
                case 0:
                    // 第一种：增加格挡（上限 1），并确保防御力为 5（构造时已设置）
                    if (this.block < MAX_BLOCK)
                    {
                        this.block++;
                        Console.WriteLine(this.name + "蜷起身躯，鳞甲闪烁（格挡 +1）！");
                    }
                    else
                    {
                        Console.WriteLine(this.name + "的鳞甲已经足够坚硬！");
                    }
                    // 防御力已经是 5，无需额外修改；如果要动态改变可以在这里设置 this.def = 5
                    break;

                case 1:
                    // 第二种：攻击一次，造成 10 点伤害
                    Attack(p);
                    break;

                case 2:
                    // 第三种：攻击三次，每次造成 3 点伤害
                    Console.WriteLine(this.name + "疯狂撕咬三次！");
                    for (int i = 0; i < 3; i++)
                    {
                        Calculator.delay(200);
                        p.takeDmg(3);
                    }
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
