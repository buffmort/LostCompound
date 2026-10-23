using Environment = LostCompound.MapEnvironment.Environment;
using System.Text;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Rooms.Types;

namespace LostCompound.Maps
{
    /**
     * 树状窄通道生成器：将二维房间网格生成为“树”形连接（无环迷宫）。
     * 每个房间有四个方向的门（上、下、左、右），通过打通相邻房间的门来建立通道。
     */
    public class TreeMazeGenerator
    {
        // 方向对应的行列偏移，按 (int)Direction 索引：上、下、左、右
        private static readonly int[] DRows = [-1, 1, 0, 0];
        private static readonly int[] DCols = [0, 0, -1, 1];

        private readonly int _rows;
        private readonly int _cols;
        private readonly Room[][] _grid;

        public TreeMazeGenerator(int rows, int cols, EnvType envType)
        {
            _rows = rows;
            _cols = cols;
            // Java: new Room[rows][cols]；C# 保持交错数组，需要逐行分配
            _grid = new Room[rows][];
            for (int r = 0; r < rows; r++)
            {
                _grid[r] = new Room[cols];
            }
            InitializeRooms(envType);
        }

        /**
         * 初始化所有房间并分配类型（START、BOSS、其他随机）。
         * 这里默认左上角为 START，右下角为 BOSS，其余随机 FIGHT/EMPTY/CRAFT。
         */
        private void InitializeRooms(EnvType envType)
        {
            // 0. 先创建一次环境对象：同一层所有房间共享同一个实例，
            //    否则水量等状态会变成每个房间各一份，互相看不见。
            Environment env = EnvironmentFactory.Create(envType);

            // 1. 固定起点和终点
            _grid[0][0] = new StartRoom(env);
            _grid[_rows - 1][_cols - 1] = new BossRoom(env);

            // 2. 收集所有可随机化的普通房间位置（排除起点和终点）
            List<int[]> normalPositions = [];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (r == 0 && c == 0) continue;
                    if (r == _rows - 1 && c == _cols - 1) continue;
                    normalPositions.Add([r, c]);
                }
            }

            // 3. 先按概率生成 FightRoom 和 EmptyRoom（Fight : Empty = 2 : 1）
            foreach (int[] pos in normalPositions)
            {
                int roll = Random.Shared.Next(3); // 0,1,2 均匀分布
                if (roll < 2)
                {
                    _grid[pos[0]][pos[1]] = new FightRoom(env);  // 2/3 概率
                }
                else
                {
                    _grid[pos[0]][pos[1]] = new EmptyRoom(env);  // 1/3 概率
                }
            }

            // 4. 50% 概率生成一个 CraftRoom，随机替换掉一个普通房间
            //    taken 记下已被替换的格子坐标：步骤 5 必须避开它们。
            List<int[]> taken = [];
            if (Random.Shared.Next(2) == 0) // 50% true / 50% false
            {
                int index = Random.Shared.Next(normalPositions.Count);
                int[] pos = normalPositions[index];
                _grid[pos[0]][pos[1]] = new CraftRoom(env);
                taken.Add(pos);
            }

            // 5. 50% 概率生成一个 TreasureRoom，随机替换掉一个普通房间（全图最多一个）
            //
            // ⚠️ 这里从「还没被步骤 4 替换过的格子」里挑，而不是再裸掷一次 normalPositions：
            //   原先两次都从同一个 normalPositions 取随机 index，取到同一格时
            //   TreasureRoom 会**覆盖** CraftRoom —— 合成台不是消失，而是整张地图直接没有合成台，
            //   而且没有任何提示（10×10 时概率约 0.26%）。
            //   现在第二次的取值域排除了已占用的格子，两者必然落在不同格。
            // 概率不变：本步仍然是独立的 50% 掷骰（掷骰在前、挑格子在后），
            //   只是「掷中时挑哪一格」的分布变成了在剩余格子上均匀。
            // 候选非空的前提：normalPositions.Count ≥ 2（10×10 时是 98），
            // 先被占掉的至多 1 格，所以 count - 1 ≥ 1，Random.Shared.Next 不会收到 0/负数。
            if (Random.Shared.Next(2) == 0)
            {
                List<int[]> available = new List<int[]>(normalPositions.Count);
                foreach (int[] p in normalPositions)
                {
                    // 同一个位置对象只可能被 taken 记一次（两处都从 normalPositions 取），
                    // 所以按坐标比较即可 —— 用引用比较会在将来有人换成 new int[] 时静默失效。
                    if (!taken.Any(t => t[0] == p[0] && t[1] == p[1]))
                    {
                        available.Add(p);
                    }
                }

                int index = Random.Shared.Next(available.Count);
                int[] pos = available[index];
                _grid[pos[0]][pos[1]] = new TreasureRoom(env);
            }
        }

        /**
         * 生成树状连接：从起始房间开始深度优先遍历，随机打通未访问的邻居。
         */
        public void Generate()
        {
            // Java: new boolean[rows][cols]；C# 保持交错数组，需要逐行分配（默认值即 false）
            bool[][] visited = new bool[_rows][];
            for (int r = 0; r < _rows; r++)
            {
                visited[r] = new bool[_cols];
            }
            Carve(0, 0, visited);
        }

        private void Carve(int row, int col, bool[][] visited)
        {
            visited[row][col] = true;
            Room current = _grid[row][col];

            List<Direction> dirs = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];
            Shuffle(dirs);

            foreach (Direction dir in dirs)
            {
                int newRow = row + DRows[(int)dir];
                int newCol = col + DCols[(int)dir];

                // 必须先检查坐标有效性，再访问 grid
                if (IsInside(newRow, newCol) && !visited[newRow][newCol])
                {
                    current.SetDoor(dir, true);
                    Room neighbor = _grid[newRow][newCol];
                    neighbor.SetDoor(Opposite(dir), true);
                    Carve(newRow, newCol, visited);
                }
            }
        }

        private bool IsInside(int row, int col)
        {
            return row >= 0 && row < _rows && col >= 0 && col < _cols;
        }

        public Room[][] Grid => _grid;

        public string ToDisplayString()
        {
            int displayRows = _rows * 2 + 1;
            int displayCols = _cols * 4 + 1;
            // Java: new char[displayRows][displayCols]；C# 保持交错数组，需要逐行分配并初始化
            char[][] display = new char[displayRows][];

            // 初始化为空格
            for (int i = 0; i < displayRows; i++)
            {
                display[i] = new char[displayCols];
                Array.Fill(display[i], ' ');
            }

            // 1. 计算可见房间（visited 及其周围 3x3）
            // Java: new boolean[rows][cols]；C# 保持交错数组，需要逐行分配（默认值即 false）
            bool[][] visible = new bool[_rows][];
            for (int r = 0; r < _rows; r++)
            {
                visible[r] = new bool[_cols];
            }
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (_grid[r][c] == null || !_grid[r][c].IsVisited) continue;

                    // 标记自身
                    MarkVisible(visible, r, c);
                    // 只沿「有门」的正交方向可见：不隔墙看、也不穿对角看。
                    // 原实现标记整个 3x3 区域，导致实体墙另一侧和对角房间都会显示为 ?。
                    Room room = _grid[r][c];
                    if (room.HasDoor(Direction.Up)) MarkVisible(visible, r - 1, c);
                    if (room.HasDoor(Direction.Down)) MarkVisible(visible, r + 1, c);
                    if (room.HasDoor(Direction.Left)) MarkVisible(visible, r, c - 1);
                    if (room.HasDoor(Direction.Right)) MarkVisible(visible, r, c + 1);
                }
            }

            // 2. 设置可见房间的中心标签
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (!visible[r][c] || _grid[r][c] == null) continue;

                    int centerRow = 2 * r + 1;
                    int centerCol = 4 * c + 2;
                    string label;
                    if (_grid[r][c].IsVisited && _grid[r][c].IsPlayerHere)
                    {
                        label = " * ";
                    }
                    else if (_grid[r][c].IsVisited)
                    {
                        label = GetRoomTypeLabel(_grid[r][c].Type);
                    }
                    else if (_grid[r][c].Type == RoomType.Boss)
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
            for (int r = 0; r <= _rows; r++)
            {
                int wallRow = 2 * r;
                for (int c = 0; c < _cols; c++)
                {
                    int wallCol = 4 * c + 1;
                    bool topVisible = (r > 0) && visible[r - 1][c];
                    bool bottomVisible = (r < _rows) && visible[r][c];
                    if (!topVisible && !bottomVisible) continue;

                    bool hasDoor = false;
                    if (topVisible) hasDoor |= _grid[r - 1][c].HasDoor(Direction.Down);
                    if (bottomVisible) hasDoor |= _grid[r][c].HasDoor(Direction.Up);

                    for (int k = 0; k < 3; k++)
                    {
                        display[wallRow][wallCol + k] = hasDoor ? ' ' : '-';
                    }
                    SetCross(display, wallRow, wallCol - 1);
                    SetCross(display, wallRow, wallCol + 3);
                }
            }

            // 4. 设置垂直墙（左右墙）
            for (int r = 0; r < _rows; r++)
            {
                int roomRow = 2 * r + 1;
                for (int c = 0; c <= _cols; c++)
                {
                    int wallCol = 4 * c;
                    bool leftVisible = (c > 0) && visible[r][c - 1];
                    bool rightVisible = (c < _cols) && visible[r][c];
                    if (!leftVisible && !rightVisible) continue;

                    bool hasDoor = false;
                    if (leftVisible) hasDoor |= _grid[r][c - 1].HasDoor(Direction.Right);
                    if (rightVisible) hasDoor |= _grid[r][c].HasDoor(Direction.Left);

                    display[roomRow][wallCol] = hasDoor ? ' ' : '|';
                }
            }

            // 5. 设置四个角的交叉点（有相邻可见房间才出现）
            for (int r = 0; r <= _rows; r++)
            {
                for (int c = 0; c <= _cols; c++)
                {
                    int crossRow = 2 * r;
                    int crossCol = 4 * c;
                    bool upLeft = (r > 0 && c > 0) && visible[r - 1][c - 1];
                    bool upRight = (r > 0 && c < _cols) && visible[r - 1][c];
                    bool downLeft = (r < _rows && c > 0) && visible[r][c - 1];
                    bool downRight = (r < _rows && c < _cols) && visible[r][c];

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
        private static void MarkVisible(bool[][] visible, int r, int c)
        {
            if (r >= 0 && r < visible.Length && c >= 0 && c < visible[0].Length)
            {
                visible[r][c] = true;
            }
        }

        // 房间类型 → 三字符显示标签（DebugMap 与探索地图共用，避免重复）
        private static string GetRoomTypeLabel(RoomType type)
        {
            return type switch
            {
                RoomType.Start => " S ",
                RoomType.Boss => " B ",
                RoomType.Fight => " F ",
                RoomType.Empty => " E ",
                RoomType.Craft => " C ",
                RoomType.Blank => " . ",
                RoomType.Treasure => " T ",
                _ => " ? ",
            };
        }

        // 辅助方法：设置交叉点为 '+'（如果当前位置是空格）
        private static void SetCross(char[][] display, int row, int col)
        {
            if (row >= 0 && row < display.Length && col >= 0 && col < display[0].Length)
            {
                if (display[row][col] == ' ')
                {
                    display[row][col] = '+';
                }
            }
        }

        // 反向方向映射：用于打通对面房间的门
        private static Direction Opposite(Direction d) => d switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            _ => Direction.Left,
        };

        // 辅助方法：C# 没有 Collections.shuffle，用 Fisher-Yates 手写等价实现
        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
