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
     *   · 新版只挑**一部分格子**变成房间（每层 14~26 间），房间之间隔着一格**走廊**连起来，
     *     再随机打通一些环 —— 于是有多条路可选，还有天然的死胡同
     *     （死胡同末端放宝物房 / 合成台，正是以撒的布局逻辑）。
     *
     * 数值口径（用户 2026-11-01 定，p0）：
     *   · 每层房间数 = 10 + 2×(边长-5)：7×7 → 14 间，13×13 → 26 间；10 层合计约 188 间；
     *   · 战斗房占普通房间的 **45%**（其余空房）→ 一局约 85 场战斗；
     *   · 环的比例 20%（相邻但没门的两间房里打通两成）。
     *
     * 空间布局：房间只落在「行列都是偶数」的格子上，所以任意两间房之间必然隔着一格，
     * 那格就是走廊（<see cref="CorridorRoom"/>）—— **永远不会出现 2×2 的房间块**。
     * 以撒的楼层里从来没有并排贴在一起的房间，那才是「看上去密集」的根源。
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
        ///   3 = 以撒式·稀疏走廊版（房间隔走廊 + 层尺寸 7→13 + 格子宽 5）—— 2026-11-01 p0 第二版
        /// </summary>
        public const int GeneratorVersion = 3;

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
        /// ⚠️ 稀疏走廊版里只有「行列都是偶数」的格子能当房间，所以实际可用的候选位是
        ///    `(边长+1)/2` 的平方（7×7 → 16 个，13×13 → 49 个）。返回值会被
        ///    <see cref="Generate"/> 再按候选位数量夹一次 —— 第 1~2 层 14/16 接近铺满，
        ///    到第 10 层 26/49 只剩一半，层数越大越疏。
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
        /// 生成整层（**以撒式连通房间图**）。六步，顺序不能换：
        ///   ① 先定每格是「房间 / 走廊 / 空」，保证**没有 2×2 的房间块**（以撒从不并排）；
        ///   ② 从一个池子里随机「挖」出目标数量的房间，挖的时候把路线上的格子标成走廊；
        ///   ③ 随机加环（走廊上也开门）—— 多条路可走；
        ///   ④ 删掉所有「死区」：不属于任何房间路径的空格变回空白；
        ///   ⑤ 每个只连着一个门的房间，再补一条走廊伸出去（死胡同要能挂奖励）；
        ///   ⑥ 最远的房间给 Boss；死胡同给宝物 / 合成台；其余按 45% 给战斗。
        ///
        /// 与上一版（贴在一起长成一片）的区别：
        ///   上一版房间网格是**紧密相连**的，所以看起来像蜂巢；
        ///   这一版房间四周**一律留空格或走廊**，房间是「一座座孤岛」，
        ///   靠细走廊和门连起来 —— 这才是以撒那种稀疏、能看清一张「房间图」的样子。
        /// </summary>
        public void Generate()
        {
            // ── ① 先决定每个格子是「候选房间位」还是「永久空」──
            // 规则：候选位之间的行列距离必须是偶数，这样任意两个候选位的坐标差都是偶数，
            // 中间的奇数列/行就自动成为走廊。
            // 为什么这样做：如果允许房间紧挨着，就会出现 2×2 的房间块 ——
            //   以撒的楼层里从来没有并排贴在一起的房间，那是「看上去密集」的根源。
            bool[][] candidate = new bool[_rows][];
            for (int r = 0; r < _rows; r++)
            {
                candidate[r] = new bool[_cols];
                for (int c = 0; c < _cols; c++)
                {
                    candidate[r][c] = (r % 2 == 0) && (c % 2 == 0);
                }
            }
            // 网格尺寸是奇数时，最后一行/列也会被留成走廊，没问题；
            // 尺寸不大于 2 时没有任何候选位，直接退化成「只有起点」。
            List<(int Row, int Col)> candidates = [];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (candidate[r][c]) candidates.Add((r, c));
                }
            }
            if (candidates.Count == 0)
            {
                _grid[0][0] = new StartRoom(_env);
                return;
            }

            int target = Math.Min(RoomCountForLayer(_rows, _cols), candidates.Count);

            // ── ② 随机挖房间 ──
            // 起点 = 坐标最接近地图中心的候选位
            (int Row, int Col) origin = candidates
                .OrderBy(p => Math.Abs(p.Row - _rows / 2) + Math.Abs(p.Col - _cols / 2))
                .First();

            Dictionary<(int Row, int Col), bool> isRoom = [];
            List<(int Row, int Col)> rooms = [];

            MakeRoom(origin);
            while (rooms.Count < target)
            {
                // 从「已有房间」里挑一个，朝一个随机方向走两格挖过去。
                // 走两格 = 中间那格是走廊，终点是新房间；这正是以撒「门 → 走廊 → 门」的形状。
                (int Row, int Col) from = rooms[_rng.Next(rooms.Count)];
                List<(int Row, int Col, Direction Dir)> options = [];
                foreach (Direction dir in AllDirections)
                {
                    (int Row, int Col) to = (from.Row + 2 * DRows[(int)dir], from.Col + 2 * DCols[(int)dir]);
                    if (!IsInside(to.Row, to.Col)) continue;
                    if (isRoom.ContainsKey(to)) continue;          // 已经是房间了
                    options.Add((to.Row, to.Col, dir));
                }
                if (options.Count == 0)
                {
                    // 这个房间四周都挖过了：换一个。若所有房间都没路可走就提前收工。
                    if (rooms.All(p => !HasFreeNeighbor(p, isRoom))) break;
                    continue;
                }

                (int Row, int Col, Direction Dir) pick = options[_rng.Next(options.Count)];
                MakeRoom((pick.Row, pick.Col));
                Connect(from, (pick.Row, pick.Col), pick.Dir);
            }

            // ── ③ 加环 ──
            // 只往「右 / 下」看，避免同一对房间被处理两次。环让「绕不绕」成为真决策。
            foreach ((int Row, int Col) cell in rooms)
            {
                foreach (Direction dir in RingDirections)
                {
                    (int Row, int Col) to = (cell.Row + 2 * DRows[(int)dir], cell.Col + 2 * DCols[(int)dir]);
                    if (!IsInside(to.Row, to.Col)) continue;
                    if (!isRoom.ContainsKey(to)) continue;
                    if (_grid[cell.Row][cell.Col].HasDoor(dir)) continue;
                    if (_rng.NextDouble() >= LoopChance) continue;
                    Connect(cell, to, dir);
                }
            }

            // ── ④ 删死区 ──
            // 挖房间时路过的走廊格（除了被真正用到的）会留下一堆孤立小段。
            // 规则：走廊格必须**至少连着两扇门**（一进一出），否则它就是个摆设，抹回空白。
            // 这一步让地图从「一堆零碎线条」变成「干净的房间 + 走道」。
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (candidate[r][c]) continue;              // 候选位不在这里处理
                    if (_grid[r][c].Type != RoomType.Blank) continue;
                    if (DoorCount(_grid[r][c]) < 2)
                    {
                        // 抹掉这条走廊上残留的门（只可能来自没走完的挖路）
                        ClearDoors(_grid[r][c]);
                    }
                }
            }

            // ── ⑤ 死胡同补走廊 ──
            // 目标是让**每个只连一个门的房间**都变成真正的死胡同末端：
            // 它自己就是路的尽头，宝物房 / 合成台挂在这里。
            // 以撒里这类房间是「门 → 房间」，不需要额外的走廊，所以这一步只要保证
            // 至少存在足够多的单门房间 —— 不够时把多余的环砍掉，自然就长出死胡同。
            TrimLoopsToMakeDeadEnds(rooms, minDeadEnds: 2);

            // ── ⑥ 分配类型 ──
            (int Row, int Col) bossCell = FarthestFrom(origin, rooms);

            List<(int Row, int Col)> deadEnds = [];
            foreach ((int Row, int Col) cell in rooms)
            {
                if (cell == origin) continue;
                if (cell == bossCell) continue;
                if (DoorCount(_grid[cell.Row][cell.Col]) != 1) continue;
                deadEnds.Add(cell);
            }
            Shuffle(deadEnds);

            int deadEndIndex = 0;
            foreach ((int Row, int Col) cell in rooms)
            {
                if (cell == origin) continue;
                if (cell == bossCell) continue;

                if (DoorCount(_grid[cell.Row][cell.Col]) == 1)
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

                Room normal = _rng.NextDouble() < FightRoomRatio
                    ? new FightRoom(_env)
                    : new EmptyRoom(_env);
                ReplaceRoom(cell, normal);
            }

            // 起点最后放：前面的 ReplaceRoom 会保留门，但起点必须保持 StartRoom 类型
            _grid[origin.Row][origin.Col] = CreateKeepingDoors(origin, new StartRoom(_env));
            _grid[bossCell.Row][bossCell.Col] = CreateKeepingDoors(bossCell, new BossRoom(_env));

            // ── 局部函数 ──
            void MakeRoom((int Row, int Col) cell)
            {
                Room room = new EmptyRoom(_env);
                _grid[cell.Row][cell.Col] = room;
                isRoom[cell] = true;
                rooms.Add(cell);
            }

            void Connect((int Row, int Col) from, (int Row, int Col) to, Direction dir)
            {
                // 两端开门
                _grid[from.Row][from.Col].SetDoor(dir, true);
                _grid[to.Row][to.Col].SetDoor(Opposite(dir), true);
                // 中间那格是走廊：它要开两个反向的门（进来的方向 + 出去的方向）
                (int Row, int Col) mid = (from.Row + DRows[(int)dir], from.Col + DCols[(int)dir]);
                // 走廊换成 **Type 为 null 的空壳**：既不画在地图上，玩家也不会"走进去"（它只是个门洞）。
                // 为什么不用 BlankRoom：Blank 表示「这格没有房间」，会被画成「 . 」，也参与各种判断；
                // 走廊是第三种东西 —— 有门、但不是房间。
                if (_grid[mid.Row][mid.Col].Type == RoomType.Blank)
                {
                    _grid[mid.Row][mid.Col] = new CorridorRoom(_env);
                }
                _grid[mid.Row][mid.Col].SetDoor(dir, true);
                _grid[mid.Row][mid.Col].SetDoor(Opposite(dir), true);
            }

            bool HasFreeNeighbor((int Row, int Col) cell, Dictionary<(int Row, int Col), bool> map)
            {
                foreach (Direction dir in AllDirections)
                {
                    (int Row, int Col) to = (cell.Row + 2 * DRows[(int)dir], cell.Col + 2 * DCols[(int)dir]);
                    if (!IsInside(to.Row, to.Col)) continue;
                    if (!map.ContainsKey(to)) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 死胡同不足 <paramref name="minDeadEnds"/> 个时，随机砍掉一些环，把房间变回单门。
        ///
        /// 为什么要砍：宝物房 / 合成台必须挂在**死胡同**上（进去要原路返回，才需要奖励），
        /// 而环加多了以后所有房间都至少两个门，死胡同可能一个都不剩。
        /// 砍环只会让地图变简单，不会产生孤立房间 —— 因此永远安全。
        /// </summary>
        private void TrimLoopsToMakeDeadEnds(List<(int Row, int Col)> rooms, int minDeadEnds)
        {
            int guard = 0;
            while (CountDeadEnds(rooms) < minDeadEnds && guard++ < rooms.Count * 4)
            {
                // 找一间门数 ≥ 3 的房间，砍掉它一个「不影响连通性」的门不方便判断，
                // 所以这里从最外侧的门下手：优先砍连接「两间房」的环（中间格是走廊的那种最容易认）。
                bool trimmed = false;
                foreach ((int Row, int Col) cell in rooms)
                {
                    if (DoorCount(_grid[cell.Row][cell.Col]) < 3) continue;
                    foreach (Direction dir in AllDirections)
                    {
                        if (!_grid[cell.Row][cell.Col].HasDoor(dir)) continue;
                        (int Row, int Col) mid = (cell.Row + DRows[(int)dir], cell.Col + DCols[(int)dir]);
                        if (!IsInside(mid.Row, mid.Col)) continue;
                        if (isCorridorBetweenRooms(cell, dir))
                        {
                            _grid[cell.Row][cell.Col].SetDoor(dir, false);
                            _grid[mid.Row][mid.Col].SetDoor(dir, false);
                            trimmed = true;
                            break;
                        }
                    }
                    if (trimmed) break;
                }
                if (!trimmed) break;   // 已经没有环可砍了
            }

            bool isCorridorBetweenRooms((int Row, int Col) cell, Direction dir)
            {
                (int Row, int Col) to = (cell.Row + 2 * DRows[(int)dir], cell.Col + 2 * DCols[(int)dir]);
                return IsInside(to.Row, to.Col) && _grid[to.Row][to.Col].Type != RoomType.Blank;
            }
        }

        /// <summary>房间列表里有几间是死胡同（只开一个门）。</summary>
        private int CountDeadEnds(List<(int Row, int Col)> rooms)
        {
            int count = 0;
            foreach ((int Row, int Col) cell in rooms)
            {
                if (DoorCount(_grid[cell.Row][cell.Col]) == 1) count++;
            }
            return count;
        }

        /// <summary>把一间房的门全部关掉（用于抹掉残留走廊）。</summary>
        private static void ClearDoors(Room room)
        {
            foreach (Direction dir in AllDirections)
            {
                room.SetDoor(dir, false);
            }
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
        /// 原来是 4（房间内容 3 字符，只能放下 " F "）；用户要「房间大一点」曾加到 6，
        /// 但层尺寸放大到 13×13 后 6 列宽会超出行宽（13×6+1 = 79 列，地图框上限 72），
        /// 所以 2026-11-01 定为 **5**：房间内容 4 字符（"  F "），仍是正经方框而非窄缝。
        ///
        /// 宽度账：地图文本宽 = cols × 5 + 1（13×13 → 66 列，地图框上限 72 列，够用）。
        /// </summary>
        private const int RoomCellWidth = 5;

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
                    // 只画**真正的房间**：走廊格（Type 为 null、只开门）与空白格都不画。
                    // 走廊如果也画出来，地图上会多出一堆「 . 」把房间图糊成棋盘 —— 以撒的地图上也只有房间。
                    // ⚠️ 判断必须写 `is null`：走廊的 Type 是 null，而 null != RoomType.Blank，
                    //    只判 Blank 会把走廊漏进来（它会被当成「 . 」画出来）。
                    if (_grid[r][c] is null || _grid[r][c].Type is null || _grid[r][c].Type == RoomType.Blank) continue;
                    if (!reveal && !_grid[r][c].IsVisited) continue;

                    // 标记自身
                    MarkVisible(visible, r, c);
                    // 只沿「有门」的正交方向可见：不隔墙看、也不穿对角看。
                    // 原实现标记整个 3x3 区域，导致实体墙另一侧和对角房间都会显示为 ?。
                    Room room = _grid[r][c];
                    foreach (Direction dir in AllDirections)
                    {
                        if (!room.HasDoor(dir)) continue;
                        int nr = r + DRows[(int)dir];
                        int nc = c + DCols[(int)dir];
                        if (!IsInside(nr, nc)) continue;

                        // 稀疏版里门后面先是一格**走廊**：走廊本身不画（画出来会把地图糊成棋盘），
                        // 所以要顺着门的方向**再走一格**，把走廊那头的房间标成可见（未知则显示 ?）。
                        // 这样「从这间房能看见隔壁有间房」的探索感还在。
                        if (_grid[nr][nc] is not null && _grid[nr][nc].Type is null)
                        {
                            int fr = nr + DRows[(int)dir];
                            int fc = nc + DCols[(int)dir];
                            if (IsInside(fr, fc)) MarkVisible(visible, fr, fc);
                            continue;
                        }
                        MarkVisible(visible, nr, nc);
                    }
                }
            }

            // 2. 设置可见房间的中心标签
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    if (!visible[r][c] || _grid[r][c] is null) continue;
                    // 走廊格 / 空白格**不画**：以撒的地图上只有房间，走廊只体现为两间房之间的空隙。
                    // ⚠️ 走廊的 Type 是 null，而 null != RoomType.Blank —— 少判 `is null` 会让走廊
                    //    每格都画成一个「 . 」盒子，整张图糊成棋盘（这就是第二版最初的样子）。
                    if (_grid[r][c].Type is null || _grid[r][c].Type == RoomType.Blank) continue;

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
                        label = GetRoomTypeLabel(_grid[r][c].Type ?? RoomType.Blank);
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

            // 5b. 画走廊：**只画一根连接线**，不画格子。
            //     稀疏版里两间房中间隔着一格走廊，它如果画成方框，整张图就糊成棋盘；
            //     画成「横 '-' / 竖 '|'」这一根线，读起来正是以撒那种「房间 + 一段走道」。
            //     ⚠️ 这一步必须在墙之后：墙会把整行（整列）按门洞填成空格，先画线会被盖掉。
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    Room cell = _grid[r][c];
                    if (cell is null || cell.Type is not null) continue;   // 不是走廊

                    // 走廊两端至少有一头是真房间，才值得画（否则是删死区漏下的残渣）
                    bool touchesVisible = false;
                    foreach (Direction dir in AllDirections)
                    {
                        if (!cell.HasDoor(dir)) continue;
                        int nr = r + DRows[(int)dir];
                        int nc = c + DCols[(int)dir];
                        if (IsInside(nr, nc) && visible[nr][nc]) touchesVisible = true;
                    }
                    if (!touchesVisible) continue;

                    bool horizontal = cell.HasDoor(Direction.Left) || cell.HasDoor(Direction.Right);
                    bool vertical = cell.HasDoor(Direction.Up) || cell.HasDoor(Direction.Down);
                    int centerRow = 2 * r + 1;
                    int centerCol = RoomCellWidth * c + 2;

                    if (horizontal)
                    {
                        // 横走廊：把这一格的中间一行填满，正好接通左右两间房的门洞
                        for (int k = 1; k < RoomCellWidth; k++)
                            display[centerRow][RoomCellWidth * c + k] = '-';
                    }
                    if (vertical)
                    {
                        // 竖走廊：只在走廊格自己那一行画一根 '|'。
                        // 上下两条墙的门洞本来就是空着的，线条穿过它们反而会变成「+ |  +」这种噪音。
                        display[centerRow][centerCol] = '|';
                    }
                    if (horizontal && vertical)
                        display[centerRow][centerCol] = '+';
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
