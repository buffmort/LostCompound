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
            // 顺序：先 DEF 减免，再吃盾（理由见 Player.TakeDamage / Monster.AbsorbWithShield）。
            double realDmg = Calculator.ApplyDefense(Def, dmg);
            double hpLoss = AbsorbWithShield(realDmg);
            if (hpLoss <= 0)
            {
                // 伤害全被护盾挡下：血量没掉，播报也跟着改口径，
                // 否则玩家会看到「受到 X 点伤害」但血条纹丝不动。
                Console.WriteLine(Name + "的护盾挡下了伤害！（剩余护盾:" + Shield.ToString("F1") + "）");
                Calculator.Delay(250);
                return;
            }
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
            Calculator.Delay(250);
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头、流血结算与跳过检查都由 Tools.RunBattle 统一处理：
            // 这里如果自己再结算一次流血，就会和 Tools.ApplyBuffs 重复结算（原 bug）。
            Calculator.Delay(250);
            int option = RollIndex(3);
            switch (option)
            {
                case 0:
                    Attack(p);
                    break;
                case 1:
                    // 原为 Block += 1（次数制，一次全免，太超模）；按用户裁定改为加 5 点护盾值。
                    AddShield(5);
                    Console.WriteLine(Name + " 增加了 5 点护盾！");
                    break;
                case 2:
                    break;
            }
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count);
    }
}
