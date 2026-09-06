package Rooms;

import MapEnvironment.EnvType;
import tools.Player;

public class BlankRoom extends Room {
    public BlankRoom(EnvType envType) {
        super(RoomType.BLANK,envType);
    }

    @Override
    public void onEnter(Player p) {
        // 待定房间，默认什么都不做
        System.out.println("这是一个白板房间");
    }
    @Override
    public void onLeave(Player p) {
    }
}