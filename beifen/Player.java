import java.util.ArrayList;
import java.util.Scanner;
public class Player {
    Scanner sc = new Scanner(System.in);
    private double hp = 100;
    private double MaxHp =100;
    private double atk = 20 ;
    private double str = 2;
    private String name;
    private double def = 5;
    private int block = 3;
    private ArrayList<String> Items  = new ArrayList<>();
    public void onPlayerTurn(ArrayList<Monster> monsters){
    if(!isAlive()){
        System.out.printf("你死了！");
        return;
    }
        System.out.println("=====你的回合=====");
        Calculator.delay(250);
        System.out.printf("你的血量:%.1f%n",this.hp);
        System.out.printf("你的防御:%.1f%n",this.def);
        System.out.println("你的格挡剩余:"+this.block);
        Calculator.delay(250);
        for(Monster m : monsters){
            System.out.print(m);
        }
        Calculator.delay(250);
        System.out.println();
        System.out.println("请选择操作");
        Calculator.delay(250);
        System.out.println("1,攻击    2,防御");
        int option = sc.nextInt();
        switch (option){
            case 1:
                Attack(monsters);
                break;
            case 2:
                block += 1;
                break;
            default:
                System.out.println("你输入的类型不正确！ 已自动跳过回合");
                break;
        }
    }

    public boolean isAlive(){
        return hp > 0;
    }

    public void takeDmg(double dmg){
        double realDmg = Calculator.Define(this.def,dmg);
        if(block >0){
            System.out.println("你格挡了伤害!");
            block -=1;
            return;
        }
        hp -= realDmg;
        System.out.printf("你受到%.1f点伤害!%n",realDmg);
        System.out.println();

    }
    public void Attack(ArrayList<Monster> m){
        double dmg = Calculator.Attack(this.str,this.atk);
        System.out.println("=====攻击界面=====");
        Calculator.delay(250);
        int count = 0;
        for (Monster i: m){
            System.out.print(count+"."+ " "+i+"   ");
            count++;
        }
        System.out.println("请选择怪物攻击:");
        int choice = sc.nextInt();
        if(choice < 0 || choice > m.size()-1){
            System.out.println("您的攻击无效！");
        }else{
            Monster target = m.get(choice);
            target.takeDmg(dmg);
            System.out.println("你攻击了"+target+"!");
        }


    }



}
