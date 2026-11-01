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
    ///      绝对不要写在 IsaacMazeGenerator 的构造函数里 —— 读档会重建地图，
    ///      那样「存一次读一次」层数就凭空涨一层。
    /// </summary>
    public static class GameProgress
    {
        /// <summary>第一层。层数从 1 开始（设计文档里「出现层数 [1, 5]」的写法同口径）。</summary>
        public const int FirstLayer = 1;

        /// <summary>最后一层（用户裁定：**10 层**）。打完第 10 层的 Boss 就算通关。</summary>
        public const int FinalLayer = 10;

        /// <summary>第 1 层的地图边长（用户裁定：**7×7 起**，2026-11-01 从 5 放大）。</summary>
        public const int BaseMapSize = 7;

        /// <summary>
        /// 「每两层 +1」这条规则用到的最后一层。之后改成**每层 +1**（见 <see cref="MapSizeForLayer"/>）。
        /// </summary>
        public const int TwoLayerStepUntil = 6;

        /// <summary>
        /// 第 N 层的地图尺寸（用户裁定 2026-11-01）：
        ///
        ///   L1-2 7×7   L3-4 8×8   L5-6 9×9   L7 10×10   L8 11×11   L9 12×12   L10 13×13
        ///
        /// 为什么不是「整局每两层 +1」：那只有 5 次 +1，7 + 5 只能到 11×11，
        /// 而用户要的是「13×13 收」。所以前 6 层沿用每两层 +1（让玩家在开局四层里
        /// 有充裕的时间熟悉规则），第 7 层起改成**每层 +1** —— 后半程压迫感陡增，
        /// 也让「再打一层就到底了」这件事在尺寸上看得见。
        ///
        /// 为什么不用「行先行、列后列」：方图换层时「整体放大一圈」读起来更自然，
        /// 长条形（8×7、9×8）会让玩家觉得某个方向莫名其妙地短一截。
        ///
        /// ⚠️ **宽度是硬约束**：地图文本宽 = 边长 × 5 + 1（见 IsaacMazeGenerator.RoomCellWidth）。
        ///    13×13 → 66 列，而地图框在 100 列终端里的上限是 72 列（= 100 − 2 − 26），刚好放得下。
        ///    再大（14×14 = 71 列内容 → 需要 77 列框）就会被裁掉右边一列房间。
        /// ⚠️ **高度也是硬约束**：地图文本高 = 边长 × 2 + 1（每间房占「上墙 + 内容」两行）。13×13 → 27 行，
        ///    右列还要再占 状态框6 + Buff框4 + 6 = 16 行，所以终端至少 43 行才不裁下边缘。
        ///    终端更矮时 ConsoleScreen 会把 MapRows 压小，**地图底部会被截掉**（不报错）。
        /// ⚠️ 尺寸只影响「新生成」的地图；旧存档的 Rows/Cols 存在档里，读档仍按原尺寸重建。
        /// ⚠️ **放射性区域**（未实现）的用户裁定：该环境**固定 10×10**，不吃这个公式 ——
        ///    实现它的时候要在建图处单独覆盖，而不是改这里。
        /// </summary>
        public static (int Rows, int Cols) MapSizeForLayer(int layer)
        {
            int l = Math.Max(layer, FirstLayer);
            int size = l <= TwoLayerStepUntil
                ? BaseMapSize + (l - 1) / 2                                       // 每两层 +1
                : BaseMapSize + (TwoLayerStepUntil - 1) / 2 + (l - TwoLayerStepUntil); // 每层 +1
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
            // 上下都夹住：下界是第一层（存档写坏成 0/负数），上界是 FinalLayer
            // （手改成 11 会生成一个设计上不存在的层）。见 EnterNextLayer 的注释。
            Layer = Math.Clamp(layer, FirstLayer, FinalLayer);
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
        ///   而 <see cref="Maps.IsaacMazeGenerator"/> 本来就会把它折成 32 位种子，
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
        ///
        /// ⚠️ **封顶到 <see cref="FinalLayer"/>**：目前唯一的调用点（`BossRoom`）自己用 `>=` 挡着，
        /// 但那只是一处判断。若将来多一个换层调用点、或存档被手改成 `Layer ≥ 10`，
        /// 就会生成第 11 层（`MapSizeForLayer` 越界算到 10×10），而它的 Boss 一死就直接通关 ——
        /// 那种「多出来的一层」既没有设计数值也没有怪物区间。上限写在这里，不依赖调用方自觉。
        /// </summary>
        public static void EnterNextLayer()
        {
            Layer = Math.Min(Layer + 1, FinalLayer);
        }
    }
}
