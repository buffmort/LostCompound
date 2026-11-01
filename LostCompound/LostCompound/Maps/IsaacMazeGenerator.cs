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
     *   · 新版只挑**一部分格子**变成房间（每层 10~18 间），房间之间用「随机生长」连起来，
     *     再随机打通一些墙形成**环** —— 于是有多条路可选，还有天然的死胡同
     *     （死胡同末端放宝物房 / 合成台，正是以撒的布局逻辑）。
     *
     * 数值口径（用户 2026-11-01 定，p0）：
     *   · 每层房间数 = 10 + 2×(边长-5)：5×5 → 10 间，9×9 → 18 间；10 层合计约 140 间；
     *   · 战斗房占普通房间的 **45%**（其余空房）→ 一局约 60~75 场战斗；
     *   · 环的比例 20%（相邻但没门的两间房里打通两成）。
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
        /// </summary>
        public const int GeneratorVersion = 2;

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
        /// 每层的房间总数：`10 + 2×(边长−5)` —— 5×5 → 10 间，6×6 → 12，…，9×9 → 18。
        ///
        /// 为什么不用「格子数的百分比」：格子是平方增长（25 → 81，3.2 倍），
        /// 按比例算会让第 9/10 层变成 24 间以上，一局又回到两三百场战斗。
        /// 按边长线性给，10 层合计约 140 间，正好落在一局 60~75 场战斗的目标里。
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
        /// 生成整层：**随机生长成树 → 加环 → 定 Boss（最远）→ 分配房间类型**。
        ///
        /// 四步的顺序不能换：
        ///   · 加环必须在「分配类型」之前：死胡同是按**最终**的度数算的，
        ///     先分类型再加环，会出现「宝物房其实有两个门、根本不是死胡同」；
        ///   · Boss 必须在加环之后选：环会改变距离，先选可能选出个不用深入就能到的 Boss。
        /// </summary>
        public void Generate()
        {
            int target = RoomCountForLayer(_rows, _cols);
            List<(int Row, int Col)> placed = [];

            // 起点放在**地图中心**，然后向四周生长。
            // 为什么不是 [0][0]（旧版的写法）：从角落长会全部挤在左上角，
            // 9×9 的层看起来「房间只占了左上四分之一、右下全是空白」。
            // 从中心长，房间会均匀铺开 —— 而且以撒的楼层本来也是以中心为基准铺的。
            // 起点在哪不影响玩法：JoinMap 是按 StartRoom 实例找起点的，不认坐标。
            (int Row, int Col) origin = (_rows / 2, _cols / 2);
            _grid[origin.Row][origin.Col] = new StartRoom(_env);
            placed.Add(origin);

            // ── 1. 随机生长：挑一间已有房间 → 挑它一个空地邻居 → 造房间 + 两边开门 ──
            // guard 是防死循环的：目标房间数可能大于「能连通的格子数」，
            // 或者所有已放置房间四周都被占满（此时 FreeNeighbors 返回空、continue 换一间）。
            int guard = 0;
            while (placed.Count < target && guard++ < _rows * _cols * 40)
            {
                (int Row, int Col) from = placed[_rng.Next(placed.Count)];
                List<(int Row, int Col, Direction Dir)> free = FreeNeighbors(from.Row, from.Col);
                if (free.Count == 0) continue;

                (int Row, int Col, Direction Dir) pick = free[_rng.Next(free.Count)];
                Room created = new EmptyRoom(_env);      // 类型稍后统一分配
                _grid[pick.Row][pick.Col] = created;
                placed.Add((pick.Row, pick.Col));

                _grid[from.Row][from.Col].SetDoor(pick.Dir, true);
                created.SetDoor(Opposite(pick.Dir), true);
            }

            // ── 2. 加环：只往「右 / 下」看，避免同一对房间被算两次 ──
            // 没有这一步整张图就是一棵树，跟旧版只是房间少了而已 —— 环才是「多条路」的来源。
            foreach ((int Row, int Col) cell in placed)
            {
                foreach (Direction dir in RingDirections)
                {
                    int nr = cell.Row + DRows[(int)dir];
                    int nc = cell.Col + DCols[(int)dir];
                    if (!IsInside(nr, nc)) continue;
                    if (_grid[nr][nc].Type == RoomType.Blank) continue;   // 那格没有房间
                    if (_grid[cell.Row][cell.Col].HasDoor(dir)) continue; // 已经有门

                    if (_rng.NextDouble() >= LoopChance) continue;

                    _grid[cell.Row][cell.Col].SetDoor(dir, true);
                    _grid[nr][nc].SetDoor(Opposite(dir), true);
                }
            }

            // ── 3. Boss = 离起点 BFS 最远的那间房 ──
            (int Row, int Col) bossCell = FarthestFrom(origin, placed);
            ReplaceRoom(bossCell, new BossRoom(_env));

            // ── 4. 分配类型 ──
            // 死胡同（只有一个门）放特殊房间：第一间宝物、第二间合成台，其余空房。
            // 这不是「为了像以撒而像以撒」——死胡同放奖励，才让「要不要拐进去」变成选择。
            List<(int Row, int Col)> deadEnds = [];
            foreach ((int Row, int Col) cell in placed)
            {
                if (cell == origin) continue;
                if (cell == bossCell) continue;
                if (DoorCount(_grid[cell.Row][cell.Col]) != 1) continue;
                deadEnds.Add(cell);
            }
            Shuffle(deadEnds);

            int deadEndIndex = 0;
            foreach ((int Row, int Col) cell in placed)
            {
                if (cell == origin) continue;
                if (cell == bossCell) continue;

                bool isDeadEnd = DoorCount(_grid[cell.Row][cell.Col]) == 1;
                if (isDeadEnd)
                {
                    Room special = deadEndIndex switch
                    {
                        0 => new TreasureRoom(_env),
                        1 => new CraftRoom(_env),
                        _ => new EmptyRoom(_env),
                    };
                    deadEndIndex++;
                    ReplaceRoom(cell, special);
                    continue;
                }

                // 普通房间：45% 战斗、其余空房
                Room normal = _rng.NextDouble() < FightRoomRatio
                    ? new FightRoom(_env)
                    : new EmptyRoom(_env);
                ReplaceRoom(cell, normal);
            }
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

        /// <summary>这格周围**还没有房间**的邻格（连同要开哪个方向的门）。</summary>
        private List<(int Row, int Col, Direction Dir)> FreeNeighbors(int row, int col)
        {
            List<(int Row, int Col, Direction Dir)> result = [];
            foreach (Direction dir in AllDirections)
            {
                int nr = row + DRows[(int)dir];
                int nc = col + DCols[(int)dir];
                if (!IsInside(nr, nc)) continue;
                if (_grid[nr][nc].Type != RoomType.Blank) continue;
                result.Add((nr, nc, dir));
            }
            return result;
        }

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
        /// 原来是 4（房间内容 3 字符，只能放下 " F "）—— 用户要求「房间大一点」，改成 6：
        /// 房间内容 5 字符（"  F  "），看起来是正经一个方框而不是窄缝。
        /// 代价：地图文本宽度 = cols × 6 + 1，9×9 时是 55 列（原来 37 列）。
        /// </summary>
        private const int RoomCellWidth = 6;

        public string ToDisplayString()
        {
            int displayRows = _rows * 2 + 1;
            int displayCols = _cols * RoomCellWidth + 1;
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
                    if (_grid[r][c] == null) continue;
                    // 没有房间的格子（BlankRoom）永远不画：空盒子和「 . 」只会把地图糊成一片网格，
                    // 真正要看的「哪些格子有房间、房间怎么连」反而看不清。
                    if (_grid[r][c].Type == RoomType.Blank) continue;
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

                    int centerRow = 2 * r + 1;
                    int centerCol = RoomCellWidth * c + 2;
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
                int wallRow = 2 * r;
                for (int c = 0; c < _cols; c++)
                {
                    int wallCol = RoomCellWidth * c + 1;
                    bool topVisible = (r > 0) && visible[r - 1][c];
                    bool bottomVisible = (r < _rows) && visible[r][c];
                    if (!topVisible && !bottomVisible) continue;

                    bool hasDoor = false;
                    if (topVisible) hasDoor |= _grid[r - 1][c].HasDoor(Direction.Down);
                    if (bottomVisible) hasDoor |= _grid[r][c].HasDoor(Direction.Up);

                    // 横墙占 `RoomCellWidth - 1` 个字符（右端那一格留给竖墙）。
                    for (int k = 0; k < RoomCellWidth - 1; k++)
                    {
                        display[wallRow][wallCol + k] = hasDoor ? ' ' : '-';
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
                int roomRow = 2 * r + 1;
                for (int c = 0; c <= _cols; c++)
                {
                    int wallCol = RoomCellWidth * c;
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
                    int crossCol = RoomCellWidth * c;
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
