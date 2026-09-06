package Rooms;

import MapEnvironment.EnvType;
import tools.Player;

public abstract class Room {
    public RoomType type;
    public boolean[] doors = new boolean[4]; // 0上 1下 2左 3右
    public boolean visited = false;
    public boolean cleaned = false;
    private boolean playerOnHere = false;
    public static final int UP = 0;
    public static final int DOWN = 1;
    public static final int LEFT = 2;
    public static final int RIGHT = 3;
    public EnvType EnvType;

    public Room(RoomType type,EnvType type1) {
        this.type = type;
        this.EnvType = type1;
    }

    // 子类实现具体的进入事件
    public abstract void onEnter(Player p);
    public void onLeave(Player p) {
        playerOnHere = false;
        // 可以在这里清理或触发离开事件
    }
    public boolean isPlayerOnHere() {
        return playerOnHere;
    }
    public void setPlayerOnHere(boolean b) {
        this.playerOnHere = b;
    }
    // 统一进入流程：先标记已访问，再触发事件
    public void enterSetup(Player p) {
        visited = true;
        playerOnHere = true;
        onEnter(p);
    }
    public void leaveSetup(Player p) {
        playerOnHere = false;
        onLeave(p);
    }

    // 检查某个方向是否有门
    public boolean isDoor(int direction) {
        if (direction >= 0 && direction < doors.length) {
            return doors[direction];
        }
        return false;
    }

    // 检查是否还有空闲门（供生成器使用，保留）
    public boolean hasFreeDoor() {
        for (boolean d : doors) {
            if (!d) return true;
        }
        return false;
    }

    // 设置门状态
    public void setDoor(int direction, boolean hasDoor) {
        if (direction >= 0 && direction < doors.length) {
            doors[direction] = hasDoor;
        }
    }

    public RoomType getRoomType() {
        return type;
    }
    public boolean isVisited() {
        return visited;
    }
    public void setVisited(boolean visited) {
        this.visited = visited;
    }
}