package monsters;

/**
 * Author:  Hyberty
 * OOP牛逼
 */

import tools.Player;

import java.util.Random;

/**
 * 作孽双子 - p1（3技能）
 */
public class TwistedPairP1 extends Monster {
    private final TwistedPairState state;
    Random rd = new Random();

    public TwistedPairP1(TwistedPairState state) {
        this.name = "Twisted Pair P1";
        this.hp = 30;
        this.def = 3;
        this.state = state;
        // 其他属性默认即可
    }

    @Override
    public void Attack(Player p) {
        // 普通攻击（若需要）
        p.takeDmg(10);
    }

    @Override
    public void takeDmg(double dmg) {
        if (block > 0) {
            block--;
            return;
        }
        hp -= dmg;
    }

    @Override
    public void onMonsterTrun(Player p) {
        System.out.println("====="+this.name+"回合=====");
        state.checkAndResetIfBothTrue();  // 新回合检测


        int choice = rdChoice(3); // 3个技能
        switch (choice) {
            case 1:
                System.out.println(this.name+"攻击了你!");
                state.p1AttackedThisTurn = true;// 标记p1已行动
                p.takeDmg(10);
                break;
            case 2:
                System.out.println(this.name+"向你喷了火!");
                System.out.println("你受到持续5回合的3点DOT伤害!");
                p.addDOTdmg(3);
                p.addDOTdur(5);
                break;
            case 3:
                System.out.println(this.name+"困住了你！");
                System.out.println("你下一回合无法行动!");
                p.setSkipTurn(1);
                break;
        }
    }

    @Override
    protected int rdChoice(int i) {
        return rd.nextInt(i) + 1;
    }

    @Override
    public boolean isAlive() {
        return hp > 0;
    }
}