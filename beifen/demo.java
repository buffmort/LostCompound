import java.util.ArrayList;
public class demo{
    public static void main(String[] args) {
        Player p = new Player();
        ArrayList<Monster> monsters = new ArrayList<>();
        Monster zb1 = new Zombie();
        Monster zb2 = new Zombie(15,1,10,1,"构造僵尸");
        monsters.add(zb1);
        monsters.add(zb2);
        Tools.Fight(p,monsters);
    }

}