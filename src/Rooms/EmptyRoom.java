package Rooms;

import MapEnvironment.EnvType;
import tools.Player;
import items.Item;
import java.util.ArrayList;
import java.util.List;

public class EmptyRoom extends Room {
    private static final List<Item> possibleItems = new ArrayList<>();


    public EmptyRoom(EnvType envType) {

        super(RoomType.EMPTY, envType);
        possibleItems.addAll(LootTables.getLootTable(this.EnvType));
    }

    @Override
    public void onEnter(Player p) {
        System.out.println("这里没有怪物，可能找到些道具。");

        if (!cleaned) {
            // 60% 概率生成随机道具
            if (Math.random() < 0.6 && !possibleItems.isEmpty()) {
                int index = (int) (Math.random() * possibleItems.size());
                Item item = possibleItems.get(index);
                p.addItem(item);
                System.out.println("你找到了 " + item.getName() + "！");
            } else {
                System.out.println("你搜刮了一番，但什么也没找到。");
            }
            // 标记为已清理，避免重复获取
            cleaned = true;
        } else {
            System.out.println("这里已经被搜过了。");
        }
    }

    @Override
    public void onLeave(Player p) {
    }
}