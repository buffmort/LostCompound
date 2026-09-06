package Rooms;

import MapEnvironment.EnvType;
import monsters.Dummy;
import monsters.LavaSlime;
import monsters.Monster;
import monsters.Zombie;
import java.util.ArrayList;
import java.util.Random;

public class MobSpawner {
    private static final Random random = new Random();
    /**
     * 随机返回 3 种不同的起始道具，供玩家选择 1 种。
     * 根据权重分配概率，药和咖啡不参与。
     *
     * @return 包含 3 种道具名称的 ArrayList
     */
    static Random rd = new Random();
    public static ArrayList<Monster> spawn(EnvType envType) {
        switch (envType) {
            case LAVA_WATERFALL:
                double num = rd.nextDouble();
                if(num<0.5) {
                        ArrayList<Monster> m = new ArrayList<>();
                        m.add(new Zombie());
                        return m;
                }else {
                    ArrayList<Monster> m = new ArrayList<>();
                    m.add(new LavaSlime());
                    return m;
                }
            default:
                // 默认返回空数组或普通怪物
                ArrayList<Monster> m = new ArrayList<>();
                m.add(new Dummy());
                return m;
        }
    }

}
