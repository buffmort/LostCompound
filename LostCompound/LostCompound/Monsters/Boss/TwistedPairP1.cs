using LostCompound.Tools;
using LostCompound.Buffs;
using LostCompound.Monsters;

/**
 * Author:  Hyberty
 * OOP牛逼
 */

/**
 * 作孽双子 - p1（3技能）
 */
namespace LostCompound.Monsters.Boss
{
    public sealed class TwistedPairP1 : Monster
    {
        private readonly TwistedPairState _state;

        /// <summary>
        /// 档位：**Boss**。依据：设计文档 §6.1「2. Twisted Pair 作孽双子 —— P1」，
        /// 双子作为一个 Boss 战出现，两半都是 Boss（毒气弹的「非 Boss」判定不该对任何一半生效）。
        /// </summary>
        public override MonsterRank Rank => MonsterRank.Boss;

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
            // 兄弟俩原来是「有格挡就整个免掉、连 DEF 都不算」，现在统一走基类的扣盾扣血。
            // 注意这里故意不过 Calculator.ApplyDefense：原实现就是裸 dmg，保持行为不变。
            AbsorbWithShield(dmg);
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
                    // 原先是 DOT 3 / 5 回合；DOT 机制已删除，改为等值的流血（唯一输出变化是这句文案）
                    Console.WriteLine("你受到持续5回合的3点流血伤害!");
                    p.AddBuff(new BleedBuff(3, 5));
                    break;
                case 3:
                    Console.WriteLine(Name + "困住了你！");
                    Console.WriteLine("你下一回合无法行动!");
                    // 走 AddSkipTurn（累加语义，见 CONVENTIONS 2.3）而不是裸写 p.SkipTurn = 1：
                    // 本技能每个怪物回合都可能抽到，「困住两次就该被跳两回合」，
                    // 赋值写法会把第二次覆盖成 1。技能文案「你下一回合无法行动」不变。
                    p.AddSkipTurn(1);
                    break;
            }
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count) + 1;
    }
}
