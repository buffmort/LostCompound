using LostCompound.Tools;

namespace LostCompound.Monsters
{
    public sealed class Dummy : Monster
    {
        public Dummy()
        {
            Type = MonsterType.Dummy;
            Name = "测试假人";
            Hp = 1000;
            Block = 0;
        }
        public override void Attack(Player p)
        {
            Console.WriteLine("假人无动于衷...");
        }

        public override void TakeDamage(double dmg)
        {
            double realDmg = Calculator.ApplyDefense(Def, dmg);
            Hp -= realDmg;
            Console.WriteLine(Name + "受到{0:F1}点伤害!", realDmg);
            Calculator.Delay(250);
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头与跳过检查由 Tools.RunBattle 统一处理
            Attack(p);
        }

        protected override int RollIndex(int count) => 0;
    }
}
