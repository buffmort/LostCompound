//package Maps;
//
//import Rooms.BlankRoom;
//import Rooms.Room;
//import Rooms.RoomType;
//import Rooms.StartRoom;
//
//import java.util.*;
//
//public class MapGenerator {
//    static Random rand;
//    static int size;
//    static Room[][] grid;
//    static int density;        // 0-100
//    static int targetRooms;
//    static int roomCount;
//
//    // 四个方向：上 下 左 右
//    static int[] dx = {0, 0, -1, 1};
//    static int[] dy = {-1, 1, 0, 0};
//    static int[] opposite = {1, 0, 3, 2};
//
//    static int countRooms(Room[][] grid) {
//        int count = 0;
//        for (int y = 0; y < grid.length; y++) {
//            for (int x = 0; x < grid[0].length; x++) {
//                if (grid[y][x] != null) count++;
//            }
//        }
//        return count;
//    }
//    public static Room[][] generate(int mapSize, int densityPercent, long seed) {
//        Room[][] bestResult = null;
//        int bestCount = 0;
//
//        for (int attempt = 0; attempt < 10; attempt++) {
//            Room[][] result = tryGenerate(mapSize, densityPercent, seed + attempt);
//            int count = countRooms(result);
//            if (count > bestCount) {
//                bestCount = count;
//                bestResult = result;
//            }
//            if (count >= (int)(size * size * density / 100.0) * 0.8) {
//                return result;
//            }
//        }
//        return bestResult;  // 返回最好的那次
//    }
//
//
//
//
//
//    public static void dfsGenerate(int x, int y, int depth) {
//        if (roomCount >= targetRooms || depth > size / 2) return;
//        if (depth > 1 && rand.nextInt(100) < 20) return;
//
//        // 这个房间只开 2-4 个门，门数决定分支量
//        int doorTarget = 2 + rand.nextInt(3);
//        List<Integer> dirs = new ArrayList<>(List.of(0, 1, 2, 3));
//        Collections.shuffle(dirs, rand);
//
//        int openedDoors = 0;
//        for (int dir : dirs) {
//            if (openedDoors >= doorTarget || roomCount >= targetRooms) return;
//
//            int nx = x + dx[dir];
//            int ny = y + dy[dir];
//
//            if (nx < 1 || nx >= size - 1 || ny < 1 || ny >= size - 1) continue;
//            if (grid[ny][nx] != null) continue;
//            if (rand.nextInt(100) >= density) continue;
//
//            Room newRoom = new BlankRoom();
//            grid[ny][nx] = newRoom;
//            openDoor(grid[y][x], newRoom, dir);
//            roomCount++;
//            openedDoors++;
//
//            dfsGenerate(nx, ny, depth + 1);
//        }
//    }
//
//
//    public static Room[][] tryGenerate(int mapSize, int densityPercent, long seed) {
//        density = densityPercent;
//        if(density <= 10){
//            density = 10;
//        }
//        rand = new Random(seed);
//        size = mapSize;
//
//        targetRooms =(int)(size * size * density / 100);
//        grid = new Room[size][size];
//
//        // 1. 中心起始房
//        int cx = size / 2;
//        int cy = size / 2;
//        grid[cy][cx] = new StartRoom();
//        grid[cy][cx].visited = true;
//        roomCount = 1;
//
//        for (int dir = 0; dir < 4; dir++) {
//            int nx = cx + dx[dir];
//            int ny = cy + dy[dir];
//            if (nx >= 1 && nx < size - 1 && ny >= 1 && ny < size - 1
//                    && grid[ny][nx] == null && rand.nextInt(100) < density) {
//                Room r = new BlankRoom();
//                grid[ny][nx] = r;
//                openDoor(grid[cy][cx], r, dir);
//                roomCount++;
//                dfsGenerate(nx, ny, 1);
//            }
//        }
//
//        // 4. 清理孤立房间
//        cleanDisconnected(cx, cy);
//
//        return grid;
//    }
//
////     上下左右 4 格全空才能建
//static boolean canPlace(int x, int y) {
//    return grid[y][x] == null;
//}
//
//    // 从起始房 BFS，删掉不可达的孤立房间
//    static void cleanDisconnected(int startX, int startY) {
//        boolean[][] reachable = new boolean[size][size];
//        Queue<int[]> q = new LinkedList<>();
//        q.add(new int[]{startX, startY});
//        reachable[startY][startX] = true;
//
//        while (!q.isEmpty()) {
//            int[] cur = q.poll();
//            Room r = grid[cur[1]][cur[0]];
//            for (int dir = 0; dir < 4; dir++) {
//                if (r.doors[dir]) {
//                    int nx = cur[0] + dx[dir];
//                    int ny = cur[1] + dy[dir];
//                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
//                    if (!reachable[ny][nx]) {
//                        reachable[ny][nx] = true;
//                        q.add(new int[]{nx, ny});
//                    }
//                }
//            }
//        }
//
//        // 删掉不可达
//        for (int y = 0; y < size; y++) {
//            for (int x = 0; x < size; x++) {
//                if (grid[y][x] != null && !reachable[y][x]) {
//                    grid[y][x] = null;
//                }
//            }
//        }
//    }
//
//    // 双向开门
//    static void openDoor(Room a, Room b, int dir) {
//        a.doors[dir] = true;
//        b.doors[opposite[dir]] = true;
//    }
//}