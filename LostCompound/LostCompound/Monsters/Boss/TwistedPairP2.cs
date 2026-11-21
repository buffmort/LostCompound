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

        /// <summary>
        /// 回合开始摇两次（PerformAction 每个回合最多跑两次：第一次 + 技能④触发的额外一次）。
        /// 必须在**玩家行动之前**摇好，否则玩家看到「打 5」实际却被连打 5 下。
        /// </summary>
        public override void PlanIntent(Player p)
        {
            SetPlannedOption(RollIndex(4));
        }

        /// <summary>
        /// 意图：普通攻击 5 / 连打 5 次各 1 / 给双子加盾 / 打 5 且可能再动一次。
        /// 「再动一次」取决于 p1 这回合有没有行动，玩家行动前就能看出来，所以如实提示。
        /// </summary>
        public override string IntentText()
        {
            if (PlannedOption == 4 && !_state.P1AttackedThisTurn)
                return "打 5 + 再动";

            return PlannedOption switch
            {
                1 => "打 5",
                2 => "打 1×5",
                3 => "盾 5",
                _ => "打 5",
            };
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
            // 第一次用 PlanIntent 摇好的；技能④触发的那次额外行动才当场摇。
            int choice = TakePlannedOption(4); // 4个技能
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
