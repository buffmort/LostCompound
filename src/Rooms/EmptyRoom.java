package Rooms;

import MapEnvironment.EnvType;
import tools.Player;

public class EmptyRoom extends Room {
    public EmptyRoom(EnvType envType) {
        super(RoomType.EMPTY,envType);
    }
    @Override
    public void onEnter(Player p) {
        System.out.println("这里没有怪物 可能找到些道具.");
    }
    @Override
    public void onLeave(Player p) {
    }
}