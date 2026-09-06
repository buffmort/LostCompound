package items;

import MapEnvironment.EnvType;
import monsters.Monster;
import tools.Calculator;
import tools.Player;

import java.util.ArrayList;

public class FushionSluphur extends Item implements Changeable , Useable, TurnCount {
    int Timer = 3;
    public FushionSluphur(int count){
        this.name = "热硫";
        this.id = 4;
        this.maxStack = 99;
        this.Stackable = true;
        this.count = count;
    }

    @Override
    public boolean ShouldRemove() {
        if(Timer > 0) {
            System.out.println("热硫将在" + Timer + "回合后消失!");
        }else{
            System.out.println("热硫已消失!");
        }
        return Timer <= 0;
    }

    @Override
    public Item Become() {
        return null;
    }

    @Override
    public ItemType getType() {
        return ItemType.FushionSluphur;
    }


    @Override
    public void use(Player p, ArrayList<Monster> m) {
        System.out.println("====你使用了热硫!====");
        Calculator.delay(250);
        int choice = p.selectTarget(m);
        if(choice == -1){
            System.out.println("你输入了错误的对象代号!");
            return;
        }
        Monster target = m.get(choice);
        target.addSkipTurn(1);
        System.out.println("你给"+target+"造成了眩晕 他跳过了一回合!");
        for(Monster monster : m){
            monster.takeDmg(10);
        }
        count -=1;
        }


    @Override
    public void ExpBattle(Player p) {

    }

    @Override
    public void onBattleStart(Player p, EnvType envType) {
        Timer -=1;
    }

    @Override
    public void onBattleEnd(Player p,EnvType envType) {

    }
}
