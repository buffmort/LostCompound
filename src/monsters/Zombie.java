package monsters;
import java.util.Random;
import tools.*;
public class Zombie extends Monster {
    public Zombie() {
        this.atk = 20;
        this.str = 0.5;
        this.def = 0;
        this.hp = 30;
        this.name = "僵尸";
    }
    public Zombie(double atk, double str, double def, double hp, String name){
            this.atk = atk;
        this.def = def;
        this.str = str;
        this.hp = hp;
        this.name = name;
    }
    Random rd = new Random();
    @Override
    public void Attack(Player p) {
        System.out.println("僵尸咬了你一口!");
        Calculator.delay(500);
        double dmg = Calculator.Attack(this.str,this.atk);
        p.takeDmg(dmg);

    }

    @Override
    public void takeDmg(double dmg){
        if(this.block>0){
            block -=1;
            return;
        }
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
        if(skipTurn >0 ){
            skipTurn -= 1;
            System.out.println("跳过回合!");
            return;
        }
        Calculator.delay(250);
    int option = rdChoice(2);
    switch (option){
        case 0:
            Attack(p);
            break;
        case 1:
            block += 1;
            System.out.println(this.name+"格挡了!");
    }
    }

    @Override
    protected int rdChoice(int i) {
        return rd.nextInt(i);
    }
    @Override
    public boolean isAlive(){
        return this.hp > 0;
    }
}
