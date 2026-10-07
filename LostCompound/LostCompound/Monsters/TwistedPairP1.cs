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
    public sealed class TwistedPairP1 : Monster
    {
        private readonly TwistedPairState _state;

        public TwistedPairP1(TwistedPairState state)
        {
            Type = MonsterType.TwistedPair1;
            Name = "Twisted Pair P1";
            Hp = 30;
            Def = 3;
            _state = state;
            // 其他属性默认即可
        }

        public override void Attack(Player p)
        {
            // 普通攻击（若需要）
            p.TakeDamage(10);
        }

        public override void TakeDamage(double dmg)
        {
            if (Block > 0)
            {
                Block--;
                return;
            }
            Hp -= dmg;
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头与跳过检查由 Tools.RunBattle 统一处理
            _state.ResetIfBothActed();  // 新回合检测


            int choice = RollIndex(3); // 3个技能
            switch (choice)
            {
                case 1:
                    Console.WriteLine(Name + "攻击了你!");
                    _state.P1AttackedThisTurn = true;// 标记p1已行动
                    p.TakeDamage(10);
                    break;
                case 2:
                    Console.WriteLine(Name + "向你喷了火!");
                    Console.WriteLine("你受到持续5回合的3点DOT伤害!");
                    p.AddDotDamage(3);
                    p.AddDotDuration(5);
                    break;
                case 3:
                    Console.WriteLine(Name + "困住了你！");
                    Console.WriteLine("你下一回合无法行动!");
                    p.SkipTurn = 1;
                    break;
            }
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count) + 1;
    }
}
