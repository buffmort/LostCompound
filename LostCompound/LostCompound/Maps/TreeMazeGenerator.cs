using System.Text;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;

namespace LostCompound.Maps
{
    /**
     * 树状窄通道生成器：将二维房间网格生成为“树”形连接（无环迷宫）。
     * 每个房间有四个方向的门（上、下、左、右），通过打通相邻房间的门来建立通道。
     */
    public class TreeMazeGenerator
    {
        // 方向常量：上、下、左、右
        private const int UP = 0;
        private const int DOWN = 1;
        private const int LEFT = 2;
        private const int RIGHT = 3;

        // 方向对应的行列偏移
        private static readonly int[] dRow = { -1, 1, 0, 0 };
        private static readonly int[] dCol = { 0, 0, -1, 1 };

        // 反向方向映射：用于打通对面房间的门
        private static readonly int[] opposite = { DOWN, UP, RIGHT, LEFT };

        private readonly int rows;
        private readonly int cols;
        private readonly Room[][] grid;
        private readonly Random random;

        public TreeMazeGenerator(int rows, int cols, EnvType envType)
            : this(rows, cols, new Random(), envType)
        {
        }

        public TreeMazeGenerator(int rows, int cols, Random random, EnvType envType)
        {
            this.rows = rows;
            this.cols = cols;
            // Java: new Room[rows][cols]；C# 保持交错数组，需要逐行分配
            this.grid = new Room[rows][];
            for (int r = 0; r < rows; r++)
            {
                this.grid[r] = new Room[cols];
            }
            this.random = random;
            initializeRooms(envType);
        }
        /**
         * 初始化所有房间并分配类型（START、BOSS、其他随机）。
         * 这里默认左上角为 START，右下角为 BOSS，其余随机 FIGHT/EMPTY/CRAFT。
         */
        private void initializeRooms(EnvType envType)
        {
            // 1. 固定起点和终点
            grid[0][0] = new StartRoom(envType);
            grid[rows - 1][cols - 1] = new BossRoom(envType);

            // 2. 收集所有可随机化的普通房间位置（排除起点和终点）
            List<int[]> normalPositions = new List<int[]>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (r == 0 && c == 0) continue;
                    if (r == rows - 1 && c == cols - 1) continue;
                    normalPositions.Add(new int[] { r, c });
                }
            }

            // 3. 先按概率生成 FightRoom 和 EmptyRoom（Fight : Empty = 2 : 1）
            foreach (int[] pos in normalPositions)
            {
                int roll = random.Next(3); // 0,1,2 均匀分布
                if (roll < 2)
                {
                    grid[pos[0]][pos[1]] = new FightRoom(envType);  // 2/3 概率
                }
                else
                {
                    grid[pos[0]][pos[1]] = new EmptyRoom(envType);  // 1/3 概率
                }
            }

            // 4. 50% 概率生成一个 CraftRoom，随机替换掉一个普通房间
            if (random.Next(2) == 0) // 50% true / 50% false
            {
                int index = random.Next(normalPositions.Count);
                int[] pos = normalPositions[index];
                grid[pos[0]][pos[1]] = new CraftRoom(EnvType.LAVA_WATERFALL);
            }
        }

        /**
         * 生成树状连接：从起始房间开始深度优先遍历，随机打通未访问的邻居。
         */
        public void generate()
        {
            // Java: new boolean[rows][cols]；C# 保持交错数组，需要逐行分配（默认值即 false）
            bool[][] visited = new bool[rows][];
            for (int r = 0; r < rows; r++)
            {
                visited[r] = new bool[cols];
            }
            dfs(0, 0, visited);
        }

        private void dfs(int row, int col, bool[][] visited)
        {
            visited[row][col] = true;
            Room current = grid[row][col];

            List<int> dirs = new List<int> { UP, DOWN, LEFT, RIGHT };
            Shuffle(dirs, random);

            foreach (int dir in dirs)
            {
                int newRow = row + dRow[dir];
                int newCol = col + dCol[dir];

                // 必须先检查坐标有效性，再访问 grid
                if (isValid(newRow, newCol) && !visited[newRow][newCol])
                {
                    current.setDoor(dir, true);
                    Room neighbor = grid[newRow][newCol];
                    neighbor.setDoor(opposite[dir], true);
                    dfs(newRow, newCol, visited);
                }
            }
        }

        private bool isValid(int row, int col)
        {
            return row >= 0 && row < rows && col >= 0 && col < cols;
        }

        public Room[][] getGrid()
        {
            return grid;
        }

        /**
         * 打印地图（文字版），显示房间类型和门连接情况。
         * 使用字符画： | 表示左右墙， - 表示上下墙， + 表示交叉点， * 表示房间。
         */
        public void DebugMap()
        {
            for (int r = 0; r < rows; r++)
            {
                // 打印上方墙壁
                for (int c = 0; c < cols; c++)
                {
                    Room room = grid[r][c];
                    Console.Write("+");
                    if (room.isDoor(UP))
                    {
                        Console.Write("   "); // 有门，通道
                    }
                    else
                    {
                        Console.Write("---");
                    }
                }
                Console.WriteLine("+");

                // 打印左墙和房间标记
                for (int c = 0; c < cols; c++)
                {
                    Room room = grid[r][c];
                    if (room.isDoor(LEFT))
                    {
                        Console.Write(" "); // 有左门，通道
                    }
                    else
                    {
                        Console.Write("|");
                    }
                    // 房间类型简写
                    string label = room.getRoomType() switch
                    {
                        RoomType.START => " S ",
                        RoomType.BOSS => " B ",
                        RoomType.FIGHT => " F ",
                        RoomType.EMPTY => " E ",
                        RoomType.CRAFT => " C ",
                        RoomType.BLANK => " . ",
                        _ => " ? ",   // 兜底：未定义枚举值时不再抛 SwitchExpressionException
                    };
                    Console.Write(label);
                }
                // 最右侧墙壁
                Console.WriteLine("|");
            }
            // 打印最下方墙壁
            for (int c = 0; c < cols; c++)
            {
                Console.Write("+---");
            }
            Console.WriteLine("+");
        }
        public Room getRoom(int row, int col)
        {
            if (row >= 0 && row < rows && col >= 0 && col < cols)
            {
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
        private string getRoomTypeChar(RoomType type)
        {
            return type switch
            {
                RoomType.START => " S ",
                RoomType.BOSS => " B ",
                RoomType.FIGHT => " F ",
                RoomType.EMPTY => " E ",
                RoomType.CRAFT => " C ",
                RoomType.BLANK => " . ",
                // 如果有其他类型请补充
                _ => " ? ",
            };
        }
        public string printMapToString()
        {
            int displayRows = rows * 2 + 1;
            int displayCols = cols * 4 + 1;
            // Java: new char[displayRows][displayCols]；C# 保持交错数组，需要逐行分配并初始化
            char[][] display = new char[displayRows][];

            // 初始化为空格
            for (int i = 0; i < displayRows; i++)
            {
                display[i] = new char[displayCols];
                for (int j = 0; j < displayCols; j++)
                {
                    display[i][j] = ' ';
                }
            }

            // 1. 计算可见房间（visited 及其周围 3x3）
            // Java: new boolean[rows][cols]；C# 保持交错数组，需要逐行分配（默认值即 false）
            bool[][] visible = new bool[rows][];
            for (int r = 0; r < rows; r++)
            {
                visible[r] = new bool[cols];
            }
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (grid[r][c] != null && grid[r][c].isVisited())
                    {
                        // 标记自身
                        setVisible(visible, r, c);
                        // 标记周围 8 个邻居（3x3 范围）
                        for (int dr = -1; dr <= 1; dr++)
                        {
                            for (int dc = -1; dc <= 1; dc++)
                            {
                                setVisible(visible, r + dr, c + dc);
                            }
                        }
                    }
                }
            }

            // 2. 设置可见房间的中心标签
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!visible[r][c] || grid[r][c] == null) continue;

                    int centerRow = 2 * r + 1;
                    int centerCol = 4 * c + 2;
                    string label;
                    if (grid[r][c].isVisited() && grid[r][c].isPlayerOnHere())
                    {
                        label = " * ";
                    }
                    else if (grid[r][c].isVisited())
                    {
                        label = getRoomTypeLabel(grid[r][c].getRoomType());
                    }
                    else if (grid[r][c].getRoomType() == RoomType.BOSS)
                    {
                        label = " B ";
                    }
                    else
                    {
                        label = " ? ";   // 未访问但可见，显示未知符号
                    }
                    display[centerRow][centerCol - 1] = label[0];
                    display[centerRow][centerCol] = label[1];
                    display[centerRow][centerCol + 1] = label[2];
                }
            }

            // 3. 设置水平墙（上下墙）
            for (int r = 0; r <= rows; r++)
            {
                int wallRow = 2 * r;
                for (int c = 0; c < cols; c++)
                {
                    int wallCol = 4 * c + 1;
                    bool topVisible = (r > 0) && visible[r - 1][c];
                    bool bottomVisible = (r < rows) && visible[r][c];
                    if (!topVisible && !bottomVisible) continue;

                    bool hasDoor = false;
                    if (topVisible) hasDoor |= grid[r - 1][c].isDoor(DOWN);
                    if (bottomVisible) hasDoor |= grid[r][c].isDoor(UP);

                    for (int k = 0; k < 3; k++)
                    {
                        display[wallRow][wallCol + k] = hasDoor ? ' ' : '-';
                    }
                    setCross(display, wallRow, wallCol - 1);
                    setCross(display, wallRow, wallCol + 3);
                }
            }

            // 4. 设置垂直墙（左右墙）
            for (int r = 0; r < rows; r++)
            {
                int roomRow = 2 * r + 1;
                for (int c = 0; c <= cols; c++)
                {
                    int wallCol = 4 * c;
                    bool leftVisible = (c > 0) && visible[r][c - 1];
                    bool rightVisible = (c < cols) && visible[r][c];
                    if (!leftVisible && !rightVisible) continue;

                    bool hasDoor = false;
                    if (leftVisible) hasDoor |= grid[r][c - 1].isDoor(RIGHT);
                    if (rightVisible) hasDoor |= grid[r][c].isDoor(LEFT);

                    display[roomRow][wallCol] = hasDoor ? ' ' : '|';
                }
            }

            // 5. 设置四个角的交叉点（有相邻可见房间才出现）
            for (int r = 0; r <= rows; r++)
            {
                for (int c = 0; c <= cols; c++)
                {
                    int crossRow = 2 * r;
                    int crossCol = 4 * c;
                    bool upLeft = (r > 0 && c > 0) && visible[r - 1][c - 1];
                    bool upRight = (r > 0 && c < cols) && visible[r - 1][c];
                    bool downLeft = (r < rows && c > 0) && visible[r][c - 1];
                    bool downRight = (r < rows && c < cols) && visible[r][c];

                    if (upLeft || upRight || downLeft || downRight)
                    {
                        display[crossRow][crossCol] = '+';
                    }
                }
            }

            // 6. 构建字符串
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < displayRows; i++)
            {
                sb.Append(new string(display[i])).Append('\n');
            }
            return sb.ToString();
        }

        // 辅助方法：安全地标记 visible 数组
        private void setVisible(bool[][] visible, int r, int c)
        {
            if (r >= 0 && r < visible.Length && c >= 0 && c < visible[0].Length)
            {
                visible[r][c] = true;
            }
        }
        private string getRoomTypeLabel(RoomType type)
        {
            return type switch
            {
                RoomType.START => " S ",
                RoomType.BOSS => " B ",
                RoomType.FIGHT => " F ",
                RoomType.EMPTY => " E ",
                RoomType.CRAFT => " C ",
                RoomType.BLANK => " . ",
                _ => " ? ",
            };
        }
        // 辅助方法：设置交叉点为 '+'（如果当前位置是空格）
        private void setCross(char[][] display, int row, int col)
        {
            if (row >= 0 && row < display.Length && col >= 0 && col < display[0].Length)
            {
                if (display[row][col] == ' ')
                {
                    display[row][col] = '+';
                }
            }
        }
        // 辅助方法：C# 没有 Collections.shuffle，用 Fisher-Yates 手写等价实现
        private static void Shuffle(List<int> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
        /*
        * 小巧思
        * 一个A*寻路找最短boss路径
        *
        * */

    }
}
