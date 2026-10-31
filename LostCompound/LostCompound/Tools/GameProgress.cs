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

        /// <summary>当前所在层数。改动请走本类的三个方法，不要直接赋值。</summary>
        public static int Layer { get; private set; } = FirstLayer;

        /// <summary>开新局：回到第一层。上一局打到哪里都不影响新局。</summary>
        public static void Reset()
        {
            Layer = FirstLayer;
        }

        /// <summary>
        /// 读档：用存档里记的层数覆盖当前值 —— **不是 +1**。
        /// 存档本来就知道玩家在第几层（<see cref="Save.SaveData.Layer"/>），照抄即可。
        /// </summary>
        public static void Restore(int layer)
        {
            Layer = Math.Max(layer, FirstLayer);   // 存档写坏了也别退回到第 0 层
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
