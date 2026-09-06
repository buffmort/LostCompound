package Maps;

import MapEnvironment.EnvType;
import Rooms.*;

import java.util.*;

/**
 * 树状窄通道生成器：将二维房间网格生成为“树”形连接（无环迷宫）。
 * 每个房间有四个方向的门（上、下、左、右），通过打通相邻房间的门来建立通道。
 */
public class TreeMazeGenerator {
    // 方向常量：上、下、左、右
    private static final int UP = 0;
    private static final int DOWN = 1;
    private static final int LEFT = 2;
    private static final int RIGHT = 3;

    // 方向对应的行列偏移
    private static final int[] dRow = {-1, 1, 0, 0};
    private static final int[] dCol = {0, 0, -1, 1};

    // 反向方向映射：用于打通对面房间的门
    private static final int[] opposite = {DOWN, UP, RIGHT, LEFT};

    private final int rows;
    private final int cols;
    private final Room[][] grid;
    private final Random random;

    public TreeMazeGenerator(int rows, int cols,EnvType envType) {
        this(rows, cols, new Random(),envType);
    }

    public TreeMazeGenerator(int rows, int cols, Random random,EnvType envType) {
        this.rows = rows;
        this.cols = cols;
        this.grid = new Room[rows][cols];
        this.random = random;
        initializeRooms(envType);
    }

    /**
     * 初始化所有房间并分配类型（START、BOSS、其他随机）。
     * 这里默认左上角为 START，右下角为 BOSS，其余随机 FIGHT/EMPTY/CRAFT。
     */
    private void initializeRooms(EnvType envType) {
        // 1. 固定起点和终点
        grid[0][0] = new StartRoom(envType);
        grid[rows - 1][cols - 1] = new BossRoom(envType);

        // 2. 收集所有可随机化的普通房间位置（排除起点和终点）
        List<int[]> normalPositions = new ArrayList<>();
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                if (r == 0 && c == 0) continue;
                if (r == rows - 1 && c == cols - 1) continue;
                normalPositions.add(new int[]{r, c});
            }
        }

        // 3. 先按概率生成 FightRoom 和 EmptyRoom（Fight : Empty = 2 : 1）
        for (int[] pos : normalPositions) {
            int roll = random.nextInt(3); // 0,1,2 均匀分布
            if (roll < 2) {
                grid[pos[0]][pos[1]] = new FightRoom(envType);  // 2/3 概率
            } else {
                grid[pos[0]][pos[1]] = new EmptyRoom(envType);  // 1/3 概率
            }
        }

        // 4. 50% 概率生成一个 CraftRoom，随机替换掉一个普通房间
        if (random.nextBoolean()) { // 50% true / 50% false
            int index = random.nextInt(normalPositions.size());
            int[] pos = normalPositions.get(index);
            grid[pos[0]][pos[1]] = new CraftRoom(EnvType.LAVA_WATERFALL);
        }
    }

    /**
     * 生成树状连接：从起始房间开始深度优先遍历，随机打通未访问的邻居。
     */
    public void generate() {
        boolean[][] visited = new boolean[rows][cols];
        dfs(0, 0, visited);
    }

    private void dfs(int row, int col, boolean[][] visited) {
        visited[row][col] = true;
        Room current = grid[row][col];

        List<Integer> dirs = new ArrayList<>(Arrays.asList(UP, DOWN, LEFT, RIGHT));
        Collections.shuffle(dirs, random);

        for (int dir : dirs) {
            int newRow = row + dRow[dir];
            int newCol = col + dCol[dir];

            // 必须先检查坐标有效性，再访问 grid
            if (isValid(newRow, newCol) && !visited[newRow][newCol]) {
                current.setDoor(dir, true);
                Room neighbor = grid[newRow][newCol];
                neighbor.setDoor(opposite[dir], true);
                dfs(newRow, newCol, visited);
            }
        }
    }

    private boolean isValid(int row, int col) {
        return row >= 0 && row < rows && col >= 0 && col < cols;
    }

    public Room[][] getGrid() {
        return grid;
    }

    /**
     * 打印地图（文字版），显示房间类型和门连接情况。
     * 使用字符画： | 表示左右墙， - 表示上下墙， + 表示交叉点， * 表示房间。
     */
    public void DebugMap() {
        for (int r = 0; r < rows; r++) {
            // 打印上方墙壁
            for (int c = 0; c < cols; c++) {
                Room room = grid[r][c];
                System.out.print("+");
                if (room.isDoor(UP)) {
                    System.out.print("   "); // 有门，通道
                } else {
                    System.out.print("---");
                }
            }
            System.out.println("+");

            // 打印左墙和房间标记
            for (int c = 0; c < cols; c++) {
                Room room = grid[r][c];
                if (room.isDoor(LEFT)) {
                    System.out.print(" "); // 有左门，通道
                } else {
                    System.out.print("|");
                }
                // 房间类型简写
                String label = switch (room.getRoomType()) {
                    case START -> " S ";
                    case BOSS  -> " B ";
                    case FIGHT -> " F ";
                    case EMPTY -> " E ";
                    case CRAFT -> " C ";
                    case BLANK -> " . ";
                };
                System.out.print(label);
            }
            // 最右侧墙壁
            System.out.println("|");
        }
        // 打印最下方墙壁
        for (int c = 0; c < cols; c++) {
            System.out.print("+---");
        }
        System.out.println("+");
    }
    public Room getRoom(int row, int col) {
        if (row >= 0 && row < rows && col >= 0 && col < cols) {
            return grid[row][col];
        }
        return null;
    }
    /**
     * 打印玩家已探索的地图（只显示访问过的房间及其相邻墙）。
     * 字符风格与 DebugMap 一致：+ 交叉点，- 水平墙，| 垂直墙。
     * 已访问房间标记为 R，玩家当前位置为 P，未访问区域为空格。
     */

    /**
     * 将房间类型转换为显示字符（与 DebugMap 风格一致）
     */
    private String getRoomTypeChar(RoomType type) {
        return switch (type) {
            case START -> " S ";
            case BOSS  -> " B ";
            case FIGHT -> " F ";
            case EMPTY -> " E ";
            case CRAFT -> " C ";
            case BLANK -> " . ";
            // 如果有其他类型请补充
        };
    }
    public String printMapToString() {
        int displayRows = rows * 2 + 1;
        int displayCols = cols * 4 + 1;
        char[][] display = new char[displayRows][displayCols];

        // 初始化为空格
        for (int i = 0; i < displayRows; i++) {
            for (int j = 0; j < displayCols; j++) {
                display[i][j] = ' ';
            }
        }

        // 1. 计算可见房间（visited 及其周围 3x3）
        boolean[][] visible = new boolean[rows][cols];
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                if (grid[r][c] != null && grid[r][c].isVisited()) {
                    // 标记自身
                    setVisible(visible, r, c);
                    // 标记周围 8 个邻居（3x3 范围）
                    for (int dr = -1; dr <= 1; dr++) {
                        for (int dc = -1; dc <= 1; dc++) {
                            setVisible(visible, r + dr, c + dc);
                        }
                    }
                }
            }
        }

        // 2. 设置可见房间的中心标签
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                if (!visible[r][c] || grid[r][c] == null) continue;

                int centerRow = 2 * r + 1;
                int centerCol = 4 * c + 2;
                String label;
                if (grid[r][c].isVisited() && grid[r][c].isPlayerOnHere()) {
                    label = " * ";
                }else if (grid[r][c].isVisited()) {
                    label = getRoomTypeLabel(grid[r][c].getRoomType());
                }else if (grid[r][c].getRoomType() == RoomType.BOSS) {
                    label = " B ";
                }else {
                    label = " ? ";   // 未访问但可见，显示未知符号
                }
                display[centerRow][centerCol - 1] = label.charAt(0);
                display[centerRow][centerCol]     = label.charAt(1);
                display[centerRow][centerCol + 1] = label.charAt(2);
            }
        }

        // 3. 设置水平墙（上下墙）
        for (int r = 0; r <= rows; r++) {
            int wallRow = 2 * r;
            for (int c = 0; c < cols; c++) {
                int wallCol = 4 * c + 1;
                boolean topVisible = (r > 0) && visible[r - 1][c];
                boolean bottomVisible = (r < rows) && visible[r][c];
                if (!topVisible && !bottomVisible) continue;

                boolean hasDoor = false;
                if (topVisible) hasDoor |= grid[r - 1][c].isDoor(DOWN);
                if (bottomVisible) hasDoor |= grid[r][c].isDoor(UP);

                for (int k = 0; k < 3; k++) {
                    display[wallRow][wallCol + k] = hasDoor ? ' ' : '-';
                }
                setCross(display, wallRow, wallCol - 1);
                setCross(display, wallRow, wallCol + 3);
            }
        }

        // 4. 设置垂直墙（左右墙）
        for (int r = 0; r < rows; r++) {
            int roomRow = 2 * r + 1;
            for (int c = 0; c <= cols; c++) {
                int wallCol = 4 * c;
                boolean leftVisible = (c > 0) && visible[r][c - 1];
                boolean rightVisible = (c < cols) && visible[r][c];
                if (!leftVisible && !rightVisible) continue;

                boolean hasDoor = false;
                if (leftVisible) hasDoor |= grid[r][c - 1].isDoor(RIGHT);
                if (rightVisible) hasDoor |= grid[r][c].isDoor(LEFT);

                display[roomRow][wallCol] = hasDoor ? ' ' : '|';
            }
        }

        // 5. 设置四个角的交叉点（有相邻可见房间才出现）
        for (int r = 0; r <= rows; r++) {
            for (int c = 0; c <= cols; c++) {
                int crossRow = 2 * r;
                int crossCol = 4 * c;
                boolean upLeft = (r > 0 && c > 0) && visible[r - 1][c - 1];
                boolean upRight = (r > 0 && c < cols) && visible[r - 1][c];
                boolean downLeft = (r < rows && c > 0) && visible[r][c - 1];
                boolean downRight = (r < rows && c < cols) && visible[r][c];

                if (upLeft || upRight || downLeft || downRight) {
                    display[crossRow][crossCol] = '+';
                }
            }
        }

        // 6. 构建字符串
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < displayRows; i++) {
            sb.append(new String(display[i])).append('\n');
        }
        return sb.toString();
    }

    // 辅助方法：安全地标记 visible 数组
    private void setVisible(boolean[][] visible, int r, int c) {
        if (r >= 0 && r < visible.length && c >= 0 && c < visible[0].length) {
            visible[r][c] = true;
        }
    }
    private String getRoomTypeLabel(RoomType type) {
        return switch (type) {
            case START -> " S ";
            case BOSS  -> " B ";
            case FIGHT -> " F ";
            case EMPTY -> " E ";
            case CRAFT -> " C ";
            case BLANK -> " . ";
        };
    }
    // 辅助方法：设置交叉点为 '+'（如果当前位置是空格）
    private void setCross(char[][] display, int row, int col) {
        if (row >= 0 && row < display.length && col >= 0 && col < display[0].length) {
            if (display[row][col] == ' ') {
                display[row][col] = '+';
            }
        }
    }
    /*
    * 小巧思
    * 一个A*寻路找最短boss路径
    *
    * */

}