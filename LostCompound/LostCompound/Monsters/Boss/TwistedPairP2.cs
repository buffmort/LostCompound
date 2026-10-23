using LostCompound.Tools;
using LostCompound.Monsters;

/**
 * Author:  Hyberty
 * OOP牛逼
 */

/**
 * 作孽双子 - p2（4技能）
 */
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Boss
{
    public sealed class TwistedPairP2 : Monster
    {
        private readonly TwistedPairState _state;
        private readonly TwistedPairP1 _brother;

        /// <summary>
        /// 档位：**Boss**。依据：设计文档 §6.1「2. Twisted Pair 作孽双子 —— P2」，
        /// 与 <see cref="TwistedPairP1"/> 同一口径（双子的两半都是 Boss）。
        /// </summary>
        public override MonsterRank Rank => MonsterRank.Boss;

        public TwistedPairP2(TwistedPairState state, TwistedPairP1 brother)
        {
            Name = "Twisted Pair P2";
            Type = MonsterType.TwistedPair2;
            Hp = 30;
            Def = 5;
            _state = state;
            _brother = brother;
        }

        public override void Attack(Player p)
        {
            p.TakeDamage(5);
        }

        public override void TakeDamage(double dmg)
        {
            // 与 P1 同一口径：不过 DEF，直接扣盾再扣血（原实现就是裸 dmg）。
            AbsorbWithShield(dmg);
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头与跳过检查由 Tools.RunBattle 统一处理
            _state.ResetIfBothActed();
            _state.P2AttackedThisTurn = true;  // 标记p2已行动

            // 第一次行动（允许触发额外行动）
            bool extraNeeded = PerformAction(p, true);
            // 如果技能④触发额外行动，则再执行一次（不允许再次额外）
            if (extraNeeded)
            {
                PerformAction(p, false);
            }
        }

        /**
         * 执行一次随机技能。
         * @param allowExtra 是否允许技能④触发额外行动
         * @return 是否触发了额外行动（用于外层判断）
         */
        private bool PerformAction(Player p, bool allowExtra)
        {
            int choice = RollIndex(4); // 4个技能
            switch (choice)
            {
                case 1:
                    Console.WriteLine(Name + "攻击了你!");
                    p.TakeDamage(5);
                    break;
                case 2:
                    // 技能2：造成1点伤害5次
                    Console.WriteLine(Name + "攻击了你5次!");
                    for (int i = 0; i < 5; i++)
                    {
                        p.TakeDamage(1);
                    }
                    break;
                case 3:
                    // 技能3：给所有怪物加 5 点护盾（原来是加 1 点「格挡次数」，已按护盾值制改写）；
                    // 若任意一个怪物已有护盾，则跳过本回合 —— 这条「已有盾就不叠加」的判定保持不变。
                    if (Shield > 0 || _brother.Shield > 0)
                    {
                        return false; // 直接不行动
                    }
                    AddShield(5); // 给自己加护盾
                    _brother.AddShield(5); // 给 p1 加护盾
                    Console.WriteLine(Name + " 给双子套上了护盾！（各 +5）");
                    break;
                case 4:
                    // 技能4：造成5伤害，若p1未攻击则额外行动一次
                    p.TakeDamage(5);
                    if (allowExtra && !_state.P1AttackedThisTurn)
                    {
                        Console.WriteLine("因为p1未行动,所以" + Name + "再行动一次!");
                        return true; // 触发额外行动
                    }
                    break;
            }
            return false;
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count) + 1; // 返回 1 ~ i
    }
}
