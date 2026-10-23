using LostCompound.Monsters;

namespace LostCompound.Monsters.Boss
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    /**
     * 双子共享状态，用于跟踪兄弟俩在本回合是否已攻击。
     * 通过检测双方都已行动来实现了简单的回合重置。
     */
    public class TwistedPairState
    {
        public bool P1AttackedThisTurn { get; set; }
        public bool P2AttackedThisTurn { get; set; }

        /**
         * 如果两个怪物都已行动（即上一回合结束），则重置标志。
         * 此方法应在每个怪物的 OnMonsterTurn 开始时调用。
         */
        public void ResetIfBothActed()
        {
            if (P1AttackedThisTurn && P2AttackedThisTurn)
            {
                P1AttackedThisTurn = false;
                P2AttackedThisTurn = false;
            }
        }
    }
}
