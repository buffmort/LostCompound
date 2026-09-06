package Rooms;

import MapEnvironment.EnvType;
import monsters.Dragon;
import monsters.Monster;
import tools.Player;
import tools.Tools;

import java.util.ArrayList;

public class FightRoom extends Room {
    private EnvType envType;   // 新增成员变量

    public FightRoom(EnvType envType) {
        super(RoomType.FIGHT, envType);
        this.envType = envType; // 保存参数到成员变量
    }

    @Override
    public void onEnter(Player p) {
        if (!cleaned) {
            System.out.println("敌人出现了!");
            Tools.Fight(p, MobSpawner.spawn(envType),this.envType);
            this.cleaned = true;
        } else {
            System.out.println("这是一个战斗过后的遗迹");
        }
    }
}