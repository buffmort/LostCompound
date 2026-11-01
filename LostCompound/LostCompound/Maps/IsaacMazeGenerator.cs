using Environment = LostCompound.MapEnvironment.Environment;
using System.Text;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Rooms.Types;
using LostCompound.Tools.Debug;   // DebugState：调试模式下全图可见（去迷雾）

namespace LostCompound.Maps
{
    /**
     * **以撒式房间生成器（IMG = Isaac Maze Generator）**。
     *
     * 与旧版（树状窄通道）的根本区别：
     *   · 旧版把**每一个格子**都变成房间，再用 DFS 挖成一棵树 —— 任意两格之间只有一条路，
     *     「要不要绕」不构成决策，而且 10 层要清约 490 间房、打约 320 场仗。
     *   · 新版只挑**一部分格子**变成房间（每层 14~26 间），再从中心随机生长、随机加环，
     *     于是有多条路可选，还有天然的死胡同
     *     （死胡同末端放宝物房 / 合成台，正是以撒的布局逻辑）。
     *
     * 数值口径（用户 2026-11-01 定，p0）：
     *   · 每层房间数 = 10 + 2×(边长-5)：7×7 → 14 间，13×13 → 26 间；10 层合计约 188 间；
     *   · 战斗房占普通房间的 **45%**（其余空房）→ 一局约 85 场战斗；
     *   · 环的比例 20%（相邻但没门的两间房里打通两成）。
     *
     * 外观的关键约束：**禁止 2×2 的房间块**。以撒的楼层里房间永远是一格宽的一条条分叉，
     * 一旦允许四间贴成方块，整张图就成了蜂窝 —— 这才是「看上去太密集」的真正来源。
     *
     * ⚠️ 没被选中的格子放 <see cref="BlankRoom"/>：它**没有门**，所以走不进去，
     *   在地图上显示成「 . 」。这是「房间少了但地图还很大」的实现方式。
     */
    public class IsaacMazeGenerator
    {
        // 方向对应的行列偏移，按 (int)Direction 索引：上、下、左、右
        private static readonly int[] DRows = [-1, 1, 0, 0];
        private static readonly int[] DCols = [0, 0, -1, 1];

        private readonly int _rows;
        private readonly int _cols;
        private readonly Room[][] _grid;

        /// <summary>
        /// 本层的环境对象（同一层所有房间**共享同一个实例** —— 水量等状态才能互相看见）。
        /// 在新版里由构造函数创建并存下来：房间类型是 <see cref="Generate"/> 里逐个分配的，
        /// 那时候已经拿不到构造参数了。
        /// </summary>
        private readonly Environment _env;

        /// <summary>
        /// 本局专用的随机源。**它是“种子 → 地形”这套存档方案的地基。**
        ///
        /// 为什么不能用 `Random.Shared`：那是进程级的全局实例，**无法播种**，
        /// 所以「同样的种子生成同样的地图」根本不成立 —— 存档里只存种子就还原不出来。
        /// 改成每个生成器持有自己的 `Random(seed)`，6 个随机点全走它，才能复现。
        ///
        /// ⚠️ 关于线程安全：`Random` 实例**不是**线程安全的，而原来的 `Random.Shared` 是。
        /// 本类只在建图时（单线程、构造之后立刻 Generate）用它，所以没有问题；
        /// 如果将来要并行建多张图，必须各自 new 一个，绝不能共用一个实例。
        /// </summary>
        private readonly Random _rng;

        /// <summary>
        /// 本局的随机种子。存档只存它 + 进度坐标，地形由它重新算出来。
        ///
        /// 类型是 **long**（64 位）：这样「时间戳类」的种子（如 `DateTime.Ticks`、
        /// `Environment.TickCount64`）可以直接丢进来，不用先想办法挤进 int。
        /// </summary>
        public long Seed { get; }

        /// <summary>
        /// 这张图属于**第几层** —— 生成时由调用方传进来的快照。
        ///
        /// 为什么要在这里存一份：`FightRoom` / `BossRoom` 要按层数出怪
        /// （盾兵 `10 × 层数` 血、怪物的出现层数区间），而地图就是 <c>Room[][]</c>，
        /// 数组上没地方挂属性；跟着「这张图是怎么来的」一起记在这里最省事。
        ///
        /// ⚠️ 不要把它当权威值去改：它是**快照**，权威值在
        /// <see cref="LostCompound.Tools.GameProgress.Layer"/>。
        /// 换层时先 <c>GameProgress.EnterNextLayer()</c>，再用新层数造一张新图，
        /// 两者就自然一致；反过来「只改这里」不会影响任何刷怪逻辑。
        /// </summary>
        public int Layer { get; }

        /// <summary>
        /// 生成算法的版本号。**存档里会存一份，读档时比对。**
        ///
        /// 为什么必须有：种子的全部魔力就是「同一种子 + 同一份算法 = 同一张图」。
        /// 以后只要动了这里的任何一个掷点顺序、概率、房间比例，旧存档的种子就会
        /// 算出**另一张地图** —— 玩家可能站在一堵新生成的墙里，或者宝箱房凭空消失，
        /// 而且全程不报错。
        ///
        /// 所以：**只要改了本类的建图逻辑，就把这个数字 +1**，
        /// 旧档会被明确拒绝，而不是安静地变成另一张图。
        ///
        /// 版本历史：
        ///   1 = 旧的树状窄通道（每个格子都是房间 + DFS 挖通）
        ///   2 = 以撒式（挑格子生长 + 加环 + 45% 战斗房）—— 2026-11-01 p0 重做
        ///   3 = 以撒式·稀疏走廊版（房间隔走廊 + 层尺寸 7→13 + 格子宽 5）—— 2026-11-01 当天被用户否掉
        ///   4 = 以撒式·**无走廊**版（房间相邻 + 禁止 2×2 房间块 + 层尺寸 7→13 + 格子宽 5）
        /// </summary>
        public const int GeneratorVersion = 4;

        public IsaacMazeGenerator(int rows, int cols, EnvType envType, long seed, int layer)
        {
            _rows = rows;
            _cols = cols;
            Seed = seed;
            Layer = Math.Max(layer, 1);   // 传 0 / 负数当第 1 层，别让刷怪逻辑拿到 0
            _rng = new Random(ToRngSeed(seed));
            // Java: new Room[rows][cols]；C# 保持交错数组，需要逐行分配
            _grid = new Room[rows][];
            for (int r = 0; r < rows; r++)
            {
                _grid[r] = new Room[cols];
            }

            // 全场先铺成 BlankRoom（没有门、走不进去），再由 Generate() 挑出真正的房间。
            // 环境对象只创建一次，同一层所有房间共享（水量等状态才能互相看见）。
            _env = EnvironmentFactory.Create(envType);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    _grid[r][c] = new BlankRoom(_env);
                }
            }
        }

        /// <summary>
        /// 把 64 位的种子折成 `Random` 能接受的 32 位种子。
        ///
        /// 为什么要折：`System.Random` 的构造函数只收 int（这是 BCL 的限制，不是我们的选择）。
        /// 种子对外的类型仍是 <c>long</c>，存档、打印、玩家读到的都是 64 位那个原值。
        ///
        /// ⚠️ **兼容性靠这一句**：种子落在 int 范围内时**原样使用**，不做任何混合 ——
        ///   这样旧存档（当时种子是 int）读出来生成的地图与升级前**逐格一致**。
        ///   只有超出 int 范围的种子（如时间戳）才走 SplitMix64 收尾混合，
        ///   以保证 64 位输入也能均匀散到 32 位，而不是简单截断（截断会让高位全失效）。
        ///
        /// 代价（要知道）：两个不同的 long 仍可能折出同一个 int，即**理论上会撞图**。
        ///   32 位空间有约 43 亿种，实际游戏里无所谓；真要 64 位全用上，
        ///   就得换掉 System.Random、自己写一个 64 位 PRNG（如 xoshiro256**）。
        /// </summary>
        private static int ToRngSeed(long seed)
        {
            if (seed >= int.MinValue && seed <= int.MaxValue)
            {
                return (int)seed;   // 旧存档走这条，地图不变
            }

            // SplitMix64 的收尾混合：把 64 位扰动成 32 位
            ulong z = (ulong)seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return unchecked((int)z);
        }

        // ==================== 生成 ====================

        /// <summary>
        /// 每层的房间总数：`10 + 2×(边长−5)` —— 7×7 → 14 间，8×8 → 16，…，13×13 → 26。
        ///
        /// 为什么不用「格子数的百分比」：格子是平方增长（49 → 169，3.4 倍），
        /// 按比例算会让第 9/10 层变成 40 间以上，一局又回到两三百场战斗。
        /// 按边长线性给，10 层合计约 188 间（战斗房约 45% → 85 场）。
        ///
        /// ⚠️ 实际能放多少间还受「禁止 2×2 房间块」限制：格子上限大约是 `rows × cols / 2`，
        ///    14~26 间远没到上限，所以这个公式不会被约束卡住。
        /// 返回前夹到 `[4, rows*cols]`：小图别超出格子数，也别少于 4 间（起点 + Boss + 2 间）。
        /// </summary>
        public static int RoomCountForLayer(int rows, int cols)
        {
            int size = Math.Min(rows, cols);
            int target = 10 + 2 * (size - 5);
            return Math.Clamp(target, Math.Min(4, rows * cols), rows * cols);
        }

        /// <summary>战斗房占「普通房间」的比例（用户裁定 45%）。</summary>
        public const double FightRoomRatio = 0.45;

        /// <summary>加环概率：相邻但还没门的两间房，按这个概率打通（用户裁定 20%）。</summary>
        public const double LoopChance = 0.2;

        /// <summary>
        /// 生成整层（**以撒式房间图**）。三步：
        ///   ① 从地图中心随机生长出目标数量的房间，**且不许出现 2×2 的房间块**；
        ///   ② 随机加环（相邻但没门的两间房里打通两成）—— 多条路可走；
        ///   ③ 最远的房间给 Boss；死胡同给宝物 / 合成台；其余按 45% 给战斗、55% 空房。
        ///
        /// 为什么禁止 2×2 房间块：一旦允许四间房贴成一个方块，地图看上去就是一整片蜂窝 ——
        ///   而**以撒的楼层里任何位置都不会出现 2×2 的房间块**，房间永远是一格宽的一条条分叉。
        ///   这条规则比任何随机权重都更能决定「像不像以撒」，所以放在生长的第一顺位。
        ///
        /// 房间之间**没有走廊**：相邻的两间房共用一道墙，墙上有门就通，玩家一步走一间房。
        /// （2026-11-01 早先做过「房间之间隔一格走廊」的版本，用户否掉了：不要走廊。）
        /// </summary>
        public void Generate()
        {
            int target = Math.Min(RoomCountForLayer(_rows, _cols), _rows * _cols);

            // 房间占用表：生长时既要查「这格有没有房」，也要查「放了它会不会凑成 2×2」
            bool[][] occupied = new bool[_rows][];
            for (int r = 0; r < _rows; r++) occupied[r] = new bool[_cols];
            List<(int Row, int Col)> rooms = [];

            // 起点放地图正中心：从角落长会全挤在左上角，大图上看起来「房间只占了一小块」
            (int Row, int Col) origin = (_rows / 2, _cols / 2);
            Place(origin);

            int guard = 0;
            while (rooms.Count < target && guard++ < _rows * _cols * 40)
            {
                (int Row, int Col) from = rooms[_rng.Next(rooms.Count)];

                List<(int Row, int Col, Direction Dir)> options = [];
                foreach (Direction dir in AllDirections)
                {
                    int nr = from.Row + DRows[(int)dir];
                    int nc = from.Col + DCols[(int)dir];
                    if (!IsInside(nr, nc)) continue;
                    if (occupied[nr][nc]) continue;
                    if (WouldMake2x2(occupied, nr, nc)) continue;
                    options.Add((nr, nc, dir));
                }
                if (options.Count == 0) continue;      // 这间房四周都长不了，换一间

                (int Row, int Col, Direction Dir) pick = options[_rng.Next(options.Count)];
                Place((pick.Row, pick.Col));
                OpenDoor(from, pick.Dir);
            }

            // ── ② 加环 ──
            // 只往「右 / 下」看，避免同一对房间算两次。没有这一步整张图是一棵树，
            // 「要不要绕」就不构成决策。
            foreach ((int Row, int Col) cell in rooms)
            {
                foreach (Direction dir in RingDirections)
                {
                    int nr = cell.Row + DRows[(int)dir];
                    int nc = cell.Col + DCols[(int)dir];
                    if (!IsInside(nr, nc)) continue;
                    if (!occupied[nr][nc]) continue;
                    if (_grid[cell.Row][cell.Col].HasDoor(dir)) continue;
                    if (_rng.NextDouble() >= LoopChance) continue;
                    OpenDoor(cell, dir);
                }
            }

            // ── ③ 定 Boss 与房间类型 ──
            (int Row, int Col) bossCell = FarthestFrom(origin, rooms);

            // 死胡同（只有一个门）挂奖励房：先宝物、再合成台。
            // 进去要原路返回，「要不要拐这一下」才是真的选择。
            List<(int Row, int Col)> deadEnds = [];
            foreach ((int Row, int Col) cell in rooms)
            {
                if (cell == origin) continue;
                if (cell == bossCell) continue;
                if (DoorCount(_grid[cell.Row][cell.Col]) != 1) continue;
                deadEnds.Add(cell);
            }
            Shuffle(deadEnds);

            // 死胡同不够两间（环加多了会这样）时，用最靠外的房间补足 ——
            // 宝物房和合成台每层必须各有一间，不能让环把它们吃掉。
            while (deadEnds.Count < 2)
            {
                (int Row, int Col) pick = (-1, -1);
                int best = -1;
                foreach ((int Row, int Col) cell in rooms)
                {
                    if (cell == origin) continue;
                    if (cell == bossCell) continue;
                    if (deadEnds.Contains(cell)) continue;
                    int score = Math.Abs(cell.Row - origin.Row) + Math.Abs(cell.Col - origin.Col);
                    if (score > best)
                    {
                        best = score;
                        pick = cell;
                    }
                }
                if (pick.Row < 0) break;   // 房间太少，补不出来
                deadEnds.Add(pick);
            }

            foreach ((int Row, int Col) cell in rooms)
            {
                if (cell == origin)
                {
                    _grid[cell.Row][cell.Col] = CreateKeepingDoors(cell, new StartRoom(_env));
                    continue;
                }
                if (cell == bossCell)
                {
                    _grid[cell.Row][cell.Col] = CreateKeepingDoors(cell, new BossRoom(_env));
                    continue;
                }

                int index = deadEnds.IndexOf(cell);
                Room replacement = index switch
                {
                    0 => new TreasureRoom(_env),
                    1 => new CraftRoom(_env),
                    _ when index > 1 => new EmptyRoom(_env),
                    _ => _rng.NextDouble() < FightRoomRatio ? new FightRoom(_env) : new EmptyRoom(_env),
                };
                _grid[cell.Row][cell.Col] = CreateKeepingDoors(cell, replacement);
            }

            // ── 局部函数 ──
            void Place((int Row, int Col) cell)
            {
                _grid[cell.Row][cell.Col] = new EmptyRoom(_env);
                occupied[cell.Row][cell.Col] = true;
                rooms.Add(cell);
            }

            // 两间相邻的房之间开门：两边各开一半，玩家一步就能走过去
            void OpenDoor((int Row, int Col) cell, Direction dir)
            {
                int nr = cell.Row + DRows[(int)dir];
                int nc = cell.Col + DCols[(int)dir];
                _grid[cell.Row][cell.Col].SetDoor(dir, true);
                _grid[nr][nc].SetDoor(Opposite(dir), true);
            }
        }

        /// <summary>
        /// 在 (row, col) 放一间房**会不会凑出 2×2 的房间块**。
        ///
        /// 检查包含这一格的 4 个 2×2 方块：只要任何一个的另外三格都已经是房间，
        /// 放下去就凑成实心方块了 —— 以撒的楼层从不出现这种形状。
        /// 这条约束同时决定了地图「条状分叉」的外观，是整层像不像以撒的关键。
        /// </summary>
        private bool WouldMake2x2(bool[][] occupied, int row, int col)
        {
            for (int dr = -1; dr <= 0; dr++)
            {
                for (int dc = -1; dc <= 0; dc++)
                {
                    int top = row + dr;
                    int left = col + dc;
                    if (top < 0 || left < 0 || top + 1 >= _rows || left + 1 >= _cols) continue;

                    int filled = 0;
                    for (int i = 0; i < 2; i++)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            int rr = top + i;
                            int cc = left + j;
                            if (rr == row && cc == col) continue;   // 自己那一格不当邻居算
                            if (occupied[rr][cc]) filled++;
                        }
                    }
                    if (filled == 3) return true;
                }
            }
            return false;
        }

        /// <summary>新建一个房间实例，但把旧格子上的门照搬过去（换类型不能把路断掉）。</summary>
        private Room CreateKeepingDoors((int Row, int Col) cell, Room fresh)
        {
            Room old = _grid[cell.Row][cell.Col];
            foreach (Direction dir in AllDirections)
            {
                if (old.HasDoor(dir)) fresh.SetDoor(dir, true);
            }
            return fresh;
        }

        /// <summary>加环时只看「右 / 下」两个方向，防止同一对相邻房间被处理两次。</summary>
        private static readonly Direction[] RingDirections = [Direction.Right, Direction.Down];

        /// <summary>把一个格子的房间换掉，但**保留原有门**（换类型不能把路断掉）。</summary>
        private void ReplaceRoom((int Row, int Col) cell, Room replacement)
        {
            Room old = _grid[cell.Row][cell.Col];
            foreach (Direction dir in AllDirections)
            {
                if (old.HasDoor(dir)) replacement.SetDoor(dir, true);
            }
            _grid[cell.Row][cell.Col] = replacement;
        }

        /// <summary>四个方向（顺序固定，用于遍历；需要随机时另外 Shuffle）。</summary>
        private static readonly Direction[] AllDirections =
            [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

        /// <summary>这间房开了几个门（死胡同 = 1）。</summary>
        private static int DoorCount(Room room)
        {
            int count = 0;
            foreach (Direction dir in AllDirections)
            {
                if (room.HasDoor(dir)) count++;
            }
            return count;
        }

        /// <summary>
        /// 从起点做 BFS，返回**离起点最远**的已放置房间（同距离取先遇到的）。
        /// 用来定 Boss 房：保证它一定在图的另一头，玩家必须深入。
        /// 走的是门（而不是格子相邻），所以「有墙」的邻格不算连通 —— 与玩家实际能走的路径一致。
        /// </summary>
        private (int Row, int Col) FarthestFrom((int Row, int Col) origin, List<(int Row, int Col)> placed)
        {
            Dictionary<(int Row, int Col), int> distance = new() { [origin] = 0 };
            Queue<(int Row, int Col)> queue = new();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                (int Row, int Col) current = queue.Dequeue();
                Room room = _grid[current.Row][current.Col];

                foreach (Direction dir in AllDirections)
                {
                    if (!room.HasDoor(dir)) continue;
                    int nr = current.Row + DRows[(int)dir];
                    int nc = current.Col + DCols[(int)dir];
                    if (!IsInside(nr, nc)) continue;
                    if (_grid[nr][nc].Type == RoomType.Blank) continue;

                    (int Row, int Col) next = (nr, nc);
                    if (distance.ContainsKey(next)) continue;

                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }

            // 取距离最大的一格。图的连通性由「生长」保证，所以所有 placed 都应当能走到；
            // 万一有走不到的房间（守卫中断导致），它不在 distance 里，这里自然会被跳过。
            (int Row, int Col) farthest = origin;
            int best = -1;
            foreach ((int Row, int Col) cell in placed)
            {
                if (!distance.TryGetValue(cell, out int d)) continue;
                if (d > best)
                {
                    best = d;
                    farthest = cell;
                }
            }
            return farthest;
        }


        private bool IsInside(int row, int col)
        {
            return row >= 0 && row < _rows && col >= 0 && col < _cols;
        }

        public Room[][] Grid => _grid;

        /// <summary>
        /// 每格房间在图上的**字符宽度**（含右侧那道竖墙）。
        /// 原来是 4（房间内容 3 字符，只能放下 " F "）；用户要「房间大一点」曾加到 6，
        /// 但层尺寸放大到 13×13 后 6 列宽会超出行宽（13×6+1 = 79 列，地图框上限 72），
        /// 所以 2026-11-01 定为 **5**：房间内容 4 字符（"  F "），仍是正经方框而非窄缝。
        ///
        /// 宽度账：地图文本宽 = cols × 5 + 1（13×13 → 66 列，地图框上限 72 列，够用）。
        /// </summary>
        private const int RoomCellWidth = 5;

        public string ToDisplayString()
        {
            // ── 先裁掉空白行列 ──
            // 房间是从地图中心长出来的，只占中间一小块：13×13 的图里常常只有 8×8 有内容。
            // 不裁的话地图文本会带上大片空行空列 —— 既把地图框撑宽撑高、又可能直接顶出终端边界。
            // 裁切按**所有房间**的包围盒算（而不是「已访问的房间」）：
            //   用已访问算的话，边走地图边走大，框会一路抖着变宽变高。
            (int MinRow, int MaxRow, int MinCol, int MaxCol) box = RoomBounds();
            int rowOffset = box.MinRow;
            int colOffset = box.MinCol;
            int rows = box.MaxRow - box.MinRow + 1;
            int cols = box.MaxCol - box.MinCol + 1;

            int displayRows = rows * 2 + 1;
            int displayCols = cols * RoomCellWidth + 1;
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
            // 调试模式：**全图可见**（去掉迷雾）。
            // 只影响"这一次绘制"，**不改 IsVisited** —— 那是进度，会被存档采集，
            // 改了等于用调试模式永久探索了整层（而且关掉调试后迷雾再也回不来）。
            bool reveal = DebugState.Enabled;

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    // 只画**真正的房间**：空白格（BlankRoom）不画 —— 空盒子和「 . 」只会把地图糊成网格，
                    // 真正要看的「哪些格子有房间、房间怎么连」反而看不清。
                    if (_grid[r][c] == null || _grid[r][c].Type == RoomType.Blank) continue;
                    if (!reveal && !_grid[r][c].IsVisited) continue;

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

                    int centerRow = 2 * (r - rowOffset) + 1;
                    int centerCol = RoomCellWidth * (c - colOffset) + 2;
                    if (!InDisplay(centerRow, centerCol)) continue;
                    // 调试全图时把未访问的房间也按真实类型显示（否则整屏都是 ?，全图就没意义了）
                    bool known = _grid[r][c].IsVisited || reveal;
                    string label;
                    if (_grid[r][c].IsVisited && _grid[r][c].IsPlayerHere)
                    {
                        label = "  *  ";
                    }
                    else if (known)
                    {
                        label = GetRoomTypeLabel(_grid[r][c].Type);
                    }
                    else if (_grid[r][c].Type == RoomType.Boss)
                    {
                        label = "  B  ";
                    }
                    else
                    {
                        label = "  ?  ";   // 未访问但可见，显示未知符号
                    }

                    display[centerRow][centerCol - 1] = label[0];
                    display[centerRow][centerCol] = label[1];
                    display[centerRow][centerCol + 1] = label[2];
                }
            }

            // 3. 设置水平墙（上下墙）
            for (int r = 0; r <= _rows; r++)
            {
                int wallRow = 2 * (r - rowOffset);
                for (int c = 0; c < _cols; c++)
                {
                    int wallCol = RoomCellWidth * (c - colOffset) + 1;
                    bool topVisible = (r > 0) && visible[r - 1][c];
                    bool bottomVisible = (r < _rows) && visible[r][c];
                    if (!topVisible && !bottomVisible) continue;
                    if (wallRow < 0 || wallRow >= displayRows) continue;

                    bool hasDoor = false;
                    if (topVisible) hasDoor |= _grid[r - 1][c].HasDoor(Direction.Down);
                    if (bottomVisible) hasDoor |= _grid[r][c].HasDoor(Direction.Up);

                    // 横墙占 `RoomCellWidth - 1` 个字符（右端那一格留给竖墙）。
                    // 有门时**只挖中间一段**（两边仍留 '-'），画出来是「+-  -+」这种窄门洞。
                    // 为什么不能整面墙都留空：相邻两间房的墙被整段抹掉后，几间房会连成一条长条，
                    // 看起来像一整块区域而不是「一间间房 + 一道门」。
                    for (int k = 0; k < RoomCellWidth - 1; k++)
                    {
                        int col = wallCol + k;
                        if (col < 0 || col >= displayCols) continue;
                        bool isDoorGap = hasDoor && k > 0 && k < RoomCellWidth - 2;
                        display[wallRow][col] = isDoorGap ? ' ' : '-';
                    }
                    // 两个墙角：左边在 wallCol-1，右边**正好是 wallCol + RoomCellWidth - 1**
                    // （也就是那道竖墙所在的列）。原先写成 +RoomCellWidth 会多推一格，
                    // 在窄格子(4)时刚好落在下一个横墙的起点、看不出来；格子加宽(6)之后就多出一个「++」。
                    SetCross(display, wallRow, wallCol - 1);
                    SetCross(display, wallRow, wallCol + RoomCellWidth - 1);
                }
            }

            // 4. 设置垂直墙（左右墙）
            for (int r = 0; r < _rows; r++)
            {
                int roomRow = 2 * (r - rowOffset) + 1;
                if (roomRow < 0 || roomRow >= displayRows) continue;
                for (int c = 0; c <= _cols; c++)
                {
                    int wallCol = RoomCellWidth * (c - colOffset);
                    bool leftVisible = (c > 0) && visible[r][c - 1];
                    bool rightVisible = (c < _cols) && visible[r][c];
                    if (!leftVisible && !rightVisible) continue;
                    if (wallCol < 0 || wallCol >= displayCols) continue;

                    bool hasDoor = false;
                    if (leftVisible) hasDoor |= _grid[r][c - 1].HasDoor(Direction.Right);
                    if (rightVisible) hasDoor |= _grid[r][c].HasDoor(Direction.Left);

                    display[roomRow][wallCol] = hasDoor ? ' ' : '|';
                }
            }

            // 5. 设置四个角的交叉点（有相邻可见房间才出现）
            for (int r = 0; r <= _rows; r++)
            {
                int crossRow = 2 * (r - rowOffset);
                if (crossRow < 0 || crossRow >= displayRows) continue;
                for (int c = 0; c <= _cols; c++)
                {
                    int crossCol = RoomCellWidth * (c - colOffset);
                    if (crossCol < 0 || crossCol >= displayCols) continue;
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

            // 局部函数：坐标是否落在裁切后的画布内
            bool InDisplay(int rr, int cc) => rr >= 0 && rr < displayRows && cc >= 0 && cc < displayCols;
        }

        /// <summary>
        /// 所有**真房间**（不含 BlankRoom）的包围盒。地图是从中心长出来的，
        /// 外围一圈常常全是空格 —— 绘制前按这个盒子裁掉空白行列，地图文本才不会虚胖。
        /// 一张空图（没有任何房间）时退回整幅地图，避免返回一个空盒子。
        /// </summary>
        private (int MinRow, int MaxRow, int MinCol, int MaxCol) RoomBounds()
        {
            int minRow = _rows, maxRow = -1, minCol = _cols, maxCol = -1;
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (_grid[r][c] == null) continue;
                    if (_grid[r][c].Type == RoomType.Blank) continue;
                    if (r < minRow) minRow = r;
                    if (r > maxRow) maxRow = r;
                    if (c < minCol) minCol = c;
                    if (c > maxCol) maxCol = c;
                }
            }
            if (maxRow < 0) return (0, _rows - 1, 0, _cols - 1);
            return (minRow, maxRow, minCol, maxCol);
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
        // ⚠️ 必须是**实例**方法：它要用本局的 `_rng`。写成 static 就够不到种子，
        //    整个「种子 → 地形」的复现性就断了（这条编译器帮不了你，只有运行期才发现）。
        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
