package monsters;

import tools.Calculator;
import tools.Player;

public class Dummy extends Monster{
    public Dummy(){
        this.name = "测试假人";
        this.hp = 10;
        this.block = 0;
    }
    @Override
    public void Attack(Player p) {
        System.out.println("假人无动于衷...");
    }

    @Override
    public void takeDmg(double dmg) {
        double realDmg = Calculator.Define(this.def,dmg);
        hp -= realDmg;
        System.out.printf(this.name+"受到%.1f点伤害!%n",realDmg);
        Calculator.delay(250);
    }

    @Override
    public void onMonsterTrun(Player p) {
        if(this.DOTdur> 0 ){
            this.DOTdur -= 1;
            this.hp -= DOTdmg;
        }
        System.out.println("=====怪物"+this.name+"回合=====");
        if(skipTurn > 0 ){
        skipTurn -= 1;
            System.out.println("跳过回合!");
        return;
    }
        Attack(p);
    }

    @Override
    protected int rdChoice(int i) {
        return 0;
    }

    @Override
    public boolean isAlive() {
        return hp>0;
    }
}
