package tools;

import MapEnvironment.EnvType;
import Maps.*;
import Rooms.Room;
import items.*;
import monsters.*;
import Maps.*;
import java.util.ArrayList;
import java.util.Random;
import java.util.Scanner;

public class demo01 {
    static void main(String[] args) {
        TreeMazeGenerator generator = new TreeMazeGenerator(11, 13, EnvType.LAVA_WATERFALL);
        generator.generate();
        Room[][] map = generator.getGrid();
        generator.DebugMap(); // 打印地图（可选）
        Player player = new Player();
        player.startGame(map,generator); // 自动寻找 StartRoom
    }
}