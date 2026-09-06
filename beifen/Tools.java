import java.util.ArrayList;
public class Tools {
    public static void Fight(Player p, ArrayList<Monster> monsters){
        System.out.println("=======战斗开始=======");
        int count = 0;
        while(p.isAlive()&&!monsters.isEmpty()){
            System.out.println("=====第"+count+"回合=====");
            monsters.removeIf(m -> !m.isAlive());
            Calculator.delay(250);
            p.onPlayerTurn(monsters);
            for(Monster m : monsters){
                m.onMonsterTrun(p);
            }
            monsters.removeIf(m -> !m.isAlive());
            count ++;
        }
        if(p.isAlive()) {
            System.out.println("战斗结束 你赢了!");
        }else{
            System.out.println("你个猪 你死了!");
        }
    }
}
