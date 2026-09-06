package monsters;

/**
 * Author:  Hyberty
 * OOP牛逼
 */
/**
 * 双子共享状态，用于跟踪兄弟俩在本回合是否已攻击。
 * 通过检测双方都已行动来实现了简单的回合重置。
 */
public class TwistedPairState {
    boolean p1AttackedThisTurn = false;
    boolean p2AttackedThisTurn = false;

    /**
     * 如果两个怪物都已行动（即上一回合结束），则重置标志。
     * 此方法应在每个怪物的 onMonsterTurn 开始时调用。
     */
    void checkAndResetIfBothTrue() {
        if (p1AttackedThisTurn && p2AttackedThisTurn) {
            p1AttackedThisTurn = false;
            p2AttackedThisTurn = false;
        }
    }
}