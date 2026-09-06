package Rooms;

import MapEnvironment.EnvType;
import items.*;
import tools.Player;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import java.util.Scanner;

public class StartRoom extends Room {
    Scanner scanner = new Scanner(System.in);
    private boolean startItemsGiven = false;   // 防止重复发放起始道具

    public StartRoom(EnvType envType) {
        super(RoomType.START, envType);
        this.visited = true;
    }

    @Override
    public void onEnter(Player p) {
        if(!cleaned) {
            System.out.println("这里是起始房间");
            this.cleaned = true;
        } else {
            System.out.println("你回到了起始房间");
        }

        // 只在第一次进入时发放起始道具
        if (!startItemsGiven) {
            giveStartItems(p);
            startItemsGiven = true;
        }
    }

    @Override
    public void onLeave(Player p) {
        // 空实现
    }

    // ==================== 起始道具选择逻辑 ====================

    private void giveStartItems(Player p) {

        // 1. 预置的 Item 对象池（直接使用已有的 Item 实例，不现场构造）
        //    请根据你的 Item 类实际情况替换以下数组初始化方式：
        //
        //    方式一：如果 Item 类有静态常量，如 Item.WOOD_1 表示"1木头"
        //    private static final Item[] START_ITEMS = {
        //        Item.WOOD_1, Item.SULFUR_1, Item.WOOD_2, Item.SULFUR_2,
        //        Item.GLASS_JAR_1, Item.PHOSPHORUS_2, Item.PHOSPHORUS_1, Item.SAND_5
        //    };
        //
        //    方式二：如果通过 ItemType 枚举获取，如 ItemType.WOOD.getItem(1) 返回"1木头"
        //    private static final Item[] START_ITEMS = {
        //        ItemType.WOOD.getItem(1),      // 1木头
        //        ItemType.SULFUR.getItem(1),    // 1硫磺
        //        ItemType.WOOD.getItem(2),      // 2木头
        //        ItemType.SULFUR.getItem(2),    // 2硫磺
        //        ItemType.GLASS_JAR.getItem(1), // 1玻璃罐
        //        ItemType.PHOSPHORUS.getItem(2),// 2白磷（假设）
        //        ItemType.PHOSPHORUS.getItem(1),// 1红磷（假设）
        //        ItemType.SAND.getItem(5)       // 5沙子
        //    };
        //
        //    请自行替换为你的实际对象。以下为占位示例（需要你替换）：
        Item[] START_ITEMS = getStartItemArray();

        // 对应权重（与 START_ITEMS 顺序一致）
        int[] WEIGHTS = {10, 10, 8, 8, 5, 5, 5, 5};

        // 可用索引列表（初始包含所有道具索引）
        List<Integer> available = new ArrayList<>();
        for (int i = 0; i < START_ITEMS.length; i++) {
            available.add(i);
        }

        Random random = new Random();

        System.out.println("===== 初始物品选择 =====");

        // 进行三轮选择
        for (int round = 1; round <= 3; round++) {
            System.out.println("第 " + round + " 轮，请选择一种道具（输入 1、2 或 3）：");

            // 从当前可用索引中随机抽取 3 个不重复的索引
            List<Integer> roundIndices = new ArrayList<>();
            List<Integer> tempPool = new ArrayList<>(available);
            for (int i = 0; i < 3; i++) {
                // 加权随机抽取一个索引
                int totalWeight = 0;
                for (int idx : tempPool) {
                    totalWeight += WEIGHTS[idx];
                }
                int rand = random.nextInt(totalWeight);
                int selectedIdx = -1;
                int cumulative = 0;
                for (int idx : tempPool) {
                    cumulative += WEIGHTS[idx];
                    if (rand < cumulative) {
                        selectedIdx = idx;
                        break;
                    }
                }
                roundIndices.add(selectedIdx);
                tempPool.remove((Integer) selectedIdx);  // 从临时池移除，防止本轮重复
            }

            // 展示本轮三个道具
            for (int i = 0; i < roundIndices.size(); i++) {
                Item item = START_ITEMS[roundIndices.get(i)];
                System.out.println((i + 1) + ". " + item.getName()+" x"+item.count);
            }

            // 读取玩家选择（1~3）
            int choice = -1;
            boolean valid = false;
            while (!valid) {
                System.out.print("输入你的选择: ");
                if (scanner.hasNextInt()) {
                    choice = scanner.nextInt();
                    if (choice >= 1 && choice <= 3) {
                        valid = true;
                    } else {
                        System.out.println("无效，请输入 1~3。");
                    }
                } else {
                    System.out.println("无效，请输入数字。");
                    scanner.next(); // 丢弃错误输入
                }
            }

            // 获取玩家选中的道具
            int selectedIdx = roundIndices.get(choice - 1);
            Item selectedItem = START_ITEMS[selectedIdx];
            p.addItem(selectedItem);
            System.out.println("你获得了: " + selectedItem.getName());

            // 从总可用池中移除该索引，防止后续轮次再次获得相同道具
            available.remove((Integer) selectedIdx);

            System.out.println();
        }

        // 注意：不要关闭 scanner，否则可能影响后续输入
    }

    // 占位方法：你需要根据实际 Item 类的设计返回预置的 Item 对象数组
    private Item[] getStartItemArray() {
    return new Item[]{
            new Wood(1),
            new Wood(2),
            new Sulphur(2),
            new Sulphur(1),
            new GlassJar(1),
            new WhitePhosphorus(2),
            new RedPhosphorus(1),
            new Sand(5),

        };
    }
}