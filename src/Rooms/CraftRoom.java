package Rooms;

import MapEnvironment.EnvType;
import tools.Player;

public class CraftRoom extends Room {
    public CraftRoom(EnvType envType) {
        super(RoomType.CRAFT,envType);
    }

    @Override
    public void onEnter(Player p) {
        System.out.println("这里有一个合成台。");
        // 可以添加合成逻辑
    }
    @Override
    public void onLeave(Player p) {
    }
}