import java.util.Random;

public class Zombie extends Monster{
    public Zombie() {
        this.atk = 20;
        this.str = 0.5;
        this.def = 0;
        this.hp = 30;
        this.name = "僵尸";
    }
    public Zombie(double atk,double str,double def,double hp,String name){
            this.atk = atk;
        this.def = def;
        this.str = str;
        this.hp = hp;
        this.name = name;
    }
    Random rd = new Random();
    @Override
    protected void Attack(Player p) {
        System.out.println("僵尸咬了你一口!");
        Calculator.delay(500);
        double dmg = Calculator.Attack(this.str,this.atk);
        p.takeDmg(dmg);

    }

    @Override
    public void takeDmg(double dmg){
        double realDmg = Calculator.Define(this.def,dmg);
        hp -= realDmg;
        System.out.printf(this.name+"受到%.1f点伤害!%n",realDmg);
        Calculator.delay(250);
    }

    @Override
    protected void onMonsterTrun(Player p) {
        System.out.println("=====怪物"+this.name+"回合=====");
        Calculator.delay(250);
    int option = rdChoice(2);
    switch (option){
        case 0:
            Attack(p);
            break;
        case 1:
            block= true;
            System.out.println(this.name+"格挡了!");
    }
    }

    @Override
    protected int rdChoice(int i) {
        int choice = rd.nextInt(i);
        return  choice;
    }
    @Override
    public boolean isAlive(){
        return this.hp > 0;
    }
}
