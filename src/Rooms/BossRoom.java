package Rooms;

import MapEnvironment.EnvType;
import monsters.Dragon;
import monsters.Monster;
import tools.Player;
import tools.Tools;
import java.util.ArrayList;

public class BossRoom extends Room {
    public BossRoom(EnvType envType) {
        super(RoomType.BOSS,envType);
    }

    @Override
    public void onEnter(Player p) {
        if(!cleaned) {
            System.out.println("Boss出现了!");
            ArrayList<Monster> monsters = new ArrayList<Monster>();
            Dragon d = new Dragon();
            monsters.add(d);
            Tools.Fight(p,monsters);
            this.cleaned = true;
        }else {
            System.out.println("你回到了起始房间");
        }
    }

    @Override
    public void onLeave(Player p) {
    }
}