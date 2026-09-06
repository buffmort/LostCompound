package items;

import monsters.Monster;
import tools.Player;

import java.util.ArrayList;

public class RedPhosphorus extends Item implements Useable{

    public RedPhosphorus(int count){
        this.name = "红磷";
        this.count = count;
        this.id = 31;
        this.maxStack = 99;
        this.Stackable = true;
    }
    @Override
    public ItemType getType() {
        return ItemType.RED_PHOSPHORUS;
    }

    @Override
    public void use(Player p, ArrayList<Monster> m) {
    int dotDmg =10;
    int dotdur = 2;
    double chance = 0.5;
        System.out.println("你点燃了红磷!");
        System.out.println("烟雾弥漫了整个房间...");
        for (Monster monsters : m){
            monsters.setDOTdur(dotdur);
            monsters.setDOTdmg(dotDmg);
        }
        System.out.println("全部怪物受到"+dotdur+"时长的 "+ dotDmg+"伤害!");
        if(Math.random() < chance){
            p.block +=1;
            System.out.println("烟雾掩护了你！你获得了一点格挡");
        }else{
            System.out.println("红磷的烟雾并没有掩护你!");
        }
    }

    @Override
    public void ExpBattle(Player p) {
        System.out.println("你不能在非战斗下使用红磷！");
    }
}
