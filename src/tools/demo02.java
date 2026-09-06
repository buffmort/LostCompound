package tools;
import MapEnvironment.EnvType;
import Maps.*;
import Rooms.Room;
import items.*;
import monsters.*;
import Maps.*;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import java.util.Scanner;

public class demo02 {
    static void main(String[] args) {
        Player player = new Player();
// 1. 创建共享状态（记录兄弟俩本回合是否攻击）
        TwistedPairState state = new TwistedPairState();
// 2. 创建 P1（需要共享状态）
        TwistedPairP1 p1 = new TwistedPairP1(state);
// 3. 创建 P2（需要共享状态和 P1 的引用）
        TwistedPairP2 p2 = new TwistedPairP2(state, p1);
// 4. 将两个 Boss 加入怪物列表
        ArrayList<Monster> monsters = new ArrayList<>();
        monsters.add(p1);
        monsters.add(p2);
        player.addItem(new Wood(5));
        player.addItem(new Charcoal(5));
        player.addItem(new WhitePhosphorus(5));
        player.addItem(new Sulphur(5));
        player.addItem(new Sand(5));
        player.addRecipe0();
        Tools.Fight(player,monsters, EnvType.LAVA_WATERFALL);
    }

}
