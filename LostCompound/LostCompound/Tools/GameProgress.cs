namespace LostCompound.Tools
{
    /// <summary>
    /// 本局进度里**不属于玩家、也不属于地图**的那部分状态 —— 目前只有「打到第几层」。
    ///
    /// 为什么要有它：层数原来是 <c>const int CurrentLayer = 1;</c>（见 FightRoom / BossRoom），
    /// <c>const</c> 是编译期烧死的数字，运行时**没有任何东西**叫 CurrentLayer，
    /// 所以「第 3 层的盾兵该有 30 血」在语法上就不可能实现。
    ///
    /// 为什么放全局而不是挂在 Map / Room 上：
    ///   · 地图就是 <c>Room[][]</c>（一个二维数组），数组没有地方挂属性；
    ///   · 挂到每个 Room 上等于同一层存 N 份副本，早晚有一份忘了更新
    ///     （见 CONVENTIONS 2.3「重算制」的那条教训：存副本会过期，问一手不会）。
    ///
    /// ⚠️ 这是**全局可变状态**，代价要知道：
    ///   ① 谁都能写，出问题时查不到是谁改的 —— 所以这里只暴露
    ///      <see cref="Reset"/> / <see cref="Restore"/> / <see cref="EnterNextLayer"/> 三个口子，
    ///      **不提供** `Layer = 5` 这样的 setter；
    ///   ② 改它只该发生在两个时机：**读档**（<see cref="Restore"/>）与
    ///      **打完 Boss 进入下一层**（<see cref="EnterNextLayer"/>）。
    ///      绝对不要写在 TreeMazeGenerator 的构造函数里 —— 读档会重建地图，
    ///      那样「存一次读一次」层数就凭空涨一层。
    /// </summary>
    public static class GameProgress
    {
        /// <summary>第一层。层数从 1 开始（设计文档里「出现层数 [1, 5]」的写法同口径）。</summary>
        public const int FirstLayer = 1;

        /// <summary>最后一层（用户裁定：**10 层**）。打完第 10 层的 Boss 就算通关。</summary>
        public const int FinalLayer = 10;

        /// <summary>第 1 层的地图边长（用户裁定：5×5 起）。</summary>
        public const int BaseMapSize = 5;

        /// <summary>
        /// 第 N 层的地图尺寸（用户裁定，方案 **A**：**每两层，行列各 +1**）。
        ///
        ///   L1-2  5×5   L3-4  6×6   L5-6  7×7   L7-8  8×8   L9-10 9×9
        ///
        /// 为什么用 A 而不是「行先行、列后列」的 B：方图换层时「整体放大一圈」读起来更自然，
        /// 长条形（6×5、7×6）会让玩家觉得某个方向莫名其妙地短一截。
        /// 第 10 层是 9×9，比原来固定 10×10 还小一档，分栏界面的排版一定放得下。
        ///
        /// ⚠️ 尺寸只影响「新生成」的地图；旧存档的 Rows/Cols 存在档里，读档仍按原尺寸重建。
        /// ⚠️ **放射性区域**（未实现）的用户裁定：该环境**固定 10×10**，不吃这个公式 ——
        ///    实现它的时候要在建图处单独覆盖，而不是改这里。
        /// </summary>
        public static (int Rows, int Cols) MapSizeForLayer(int layer)
        {
            int size = BaseMapSize + (Math.Max(layer, FirstLayer) - 1) / 2;   // 每两层 +1
            return (size, size);
        }

        /// <summary>当前所在层数。改动请走本类的三个方法，不要直接赋值。</summary>
        public static int Layer { get; private set; } = FirstLayer;

        /// <summary>
        /// 本局的**总种子**：开新局时摇一次（`Random.Shared.NextInt64()`），之后整局不变。
        ///
        /// 它的作用是「一层一层派生子种子」的来源 —— 每层地图仍然各自有自己的种子
        /// （<see cref="SeedForLayer"/>），而不是所有层共用同一个种子（那样每层迷宫长得一样）。
        /// 存档里也要存它：读档后玩家继续往下打时，第 N+1 层的种子还得能算出来。
        /// </summary>
        public static long RunSeed { get; private set; }

        /// <summary>当前层的种子（由 <see cref="RunSeed"/> 与 <see cref="Layer"/> 派生）。</summary>
        public static long LayerSeed => SeedForLayer(RunSeed, Layer);

        /// <summary>开新局：回到第一层，并记下本局总种子。</summary>
        public static void StartRun(long runSeed)
        {
            Layer = FirstLayer;
            RunSeed = runSeed;
        }

        /// <summary>
        /// 读档：用存档里记的层数与总种子覆盖当前值 —— **不是 +1**。
        /// 存档本来就知道玩家在第几层（<see cref="Save.SaveData.Layer"/>），照抄即可。
        /// </summary>
        public static void Restore(int layer, long runSeed)
        {
            Layer = Math.Max(layer, FirstLayer);   // 存档写坏了也别退回到第 0 层
            RunSeed = runSeed;
        }

        /// <summary>
        /// 由「总种子 + 层数」算出**这一层的种子**。
        ///
        /// 公式（用户裁定）：**总种子 × 层数**。第 1 层就是总种子本身。
        ///
        /// 为什么是**纯函数**而不是「拿一个 Random 依次 NextInt64()」：后者依赖调用顺序。
        /// 玩家在第 3 层存档、退出、读档，然后下到第 4 层时，那个 Random 已经从头开始了 ——
        /// 第 4 层会拿到本该属于第 2 层的种子，两层地图重复。
        /// 纯函数没有状态：只要 (总种子, 层数) 一样，算出来就一样，读档多少次都一致。
        ///
        /// ⚠️ 乘法会**溢出**（long 只有 64 位，总种子 × 3 就可能越界）。这在 C# 里是
        ///   回绕（unchecked）而不是异常，结果仍是一个「看起来随机」的 64 位数 ——
        ///   而 <see cref="Maps.TreeMazeGenerator"/> 本来就会把它折成 32 位种子，
        ///   所以回绕不影响可用性。唯一要挡的是**乘积恰好为 0**（总种子为 0、或低位的
        ///   2 的因子被乘光）：那会让该层与后续所有层的种子都变成 0，地图全部重复。
        ///   这一句兜底把它退回总种子。
        /// </summary>
        public static long SeedForLayer(long runSeed, int layer)
        {
            long seed = unchecked(runSeed * Math.Max(layer, FirstLayer));
            return seed == 0 ? runSeed : seed;
        }

        /// <summary>开新局：回到第一层（不改变总种子，供内部/测试使用）。</summary>
        public static void Reset()
        {
            Layer = FirstLayer;
        }

        /// <summary>
        /// 打完 Boss、进入下一层。**这是全项目唯一会让层数 +1 的地方。**
        /// 调用点必须在"真的要换一张图"的那一刻 —— 只加数不换图会让第 N 层的地图
        /// 里刷出第 N+1 层的怪（见 <see cref="Restore"/> 里那条警告）。
        /// </summary>
        public static void EnterNextLayer()
        {
            Layer++;
        }
    }
}
