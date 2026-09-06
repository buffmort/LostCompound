package Rooms;

/**
 * Author:  Hyberty
 * OOP牛逼
 */
import MapEnvironment.EnvType;
import items.*;

import java.util.ArrayList;
public class LootTables {
    /**
     * 根据环境类型返回该环境下可能掉落的道具列表。
     * 返回的是一个新创建的 ArrayList，避免外部修改影响内部数据。
     */
    public static ArrayList<Item> getLootTable(EnvType envType) {
        ArrayList<Item> items = new ArrayList<>();
        switch (envType) {
            case NONE:
                System.out.println("请调试！ 这是一个空环境!");
                break;
            case LAVA_WATERFALL:
                // 熔岩瀑布环境：掉落的特殊道具
                items.add(new Sulphur(1));
                items.add(new Sulphur(2));
                items.add(new Charcoal(1));
                items.add(new WhitePhosphorus(1));
                items.add(new RedPhosphorus(1));
                items.add(new RedPhosphorus(2));
                items.add(new Wood(5));
                items.add(new Wood(5));
                items.add(new Wood(5));

                break;
            // 如果以后新增环境，在这里添加 case
            // case Mine:
            //     items.add(new WoodenBow());
            //     break;
            default:
                // 未知环境默认返回空列表
                break;
        }
        return items;
    }
}