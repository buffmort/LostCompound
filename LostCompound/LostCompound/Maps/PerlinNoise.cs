namespace LostCompound.Maps
{
    /// <summary>
    /// 二维**柏林噪声**（Ken Perlin 的改进版，1985/2002）。
    ///
    /// 用它干什么：给地图上每间房算一个「辐射强度」，范围 5~30 秒（见
    /// <see cref="Rooms.Room.RadiationSeconds"/>）。选它而不是 `Random`，
    /// 是因为它的核心性质是**连续性**：相邻的两个采样点结果相近。
    ///
    /// ## 它到底在算什么（一步步）
    ///
    /// 1. **建一张网格，每个格子角上放一个"箭头"（梯度向量）。**
    ///    我们的格子是整数坐标，角点 `(x, y)` 的箭头由置换表 `_perm` 决定：
    ///    先按 `x` 查一次，再按 `y` 查一次，得到 0~255 的一个数，
    ///    用它算出角度，就得到一个单位向量。相同坐标永远得到相同箭头 —— 这是"确定性"的来源。
    ///
    /// 2. **找到采样点落在哪个格子里。**
    ///    采样点 `(x, y)` → 整数部分 `(xi, yi)` 是格子左下角，小数部分 `(xf, yf)` 是它在这个格子内的位置。
    ///
    /// 3. **对四个角各算一次点积。**
    ///    把「采样点到该角的偏移」与「该角的箭头」做点积（就是投影）。
    ///    点积为正 = 采样点在箭头所指的那一侧；为负 = 在另一侧。
    ///    这四步点积就是全部信号，后面只是把它们**平滑混合**。
    ///
    /// 4. **在 x 方向混合上下两条边，再在 y 方向混合结果。**
    ///    混合权重不是线性的 `t`，而是 `6t⁵ - 15t⁴ + 10t³`（fade 曲线）——
    ///    因为线性插值会在格子边界留下一阶导不连续的「折痕」，屏幕上看得出来是一格一格的。
    ///    这条曲线在 0 和 1 处的一阶、二阶导数都是 0，所以格与格之间是平滑接上的。
    ///
    /// 5. **结果落在约 [-1, 1]。**
    ///    理论上完全可能超出（四角点积的加权平均），但实测极值就在 ±1 附近，
    ///    所以 <see cref="Sample01"/> 用 `(v + 1) / 2` 再夹一次到 0~1。
    ///
    /// ## 用它铺图时要注意的两件事
    ///
    /// - **频率**（<see cref="Sample01"/> 的 `frequency` 参数）决定"多长的距离内变化一次"。
    ///   频率 1 = 每 1 格就是完整的一个噪声周期（相邻房间差别巨大，看起来像随机数）；
    ///   频率 0.18 = 大约 5~6 格才走完一个周期（相邻房间接近、整张图能看出"毒气带"）。
    ///   想调"成片程度"就改这一个数。
    /// - **种子**：置换表由注入的种子洗牌得到，所以同一种子 + 同一坐标 = 同一个值。
    ///   地图不存档、读档按 `SeedForLayer` 重算，所以这里**必须**走种子而**不能**用
    ///   `Random.Shared`（否则每读一次档，毒气带就挪一次位置）。
    /// </summary>
    public sealed class PerlinNoise
    {
        /// <summary>置换表：0~255 洗牌后**首尾接一遍**（长度 512），省掉每次取模。</summary>
        private readonly int[] _perm = new int[512];

        /// <param name="seed">同一种子 + 同一坐标 = 同一个值。地图靠它做到「读档后毒气带不挪位」。</param>
        public PerlinNoise(long seed)
        {
            // 洗牌用独立的 Random（不用 Random.Shared）：怕它被别处的调用影响顺序
            Random rng = new(unchecked((int)(seed ^ (seed >> 32))));

            int[] p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            // Fisher–Yates 洗牌
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }

            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        /// <summary>原始噪声，结果约在 [-1, 1]。</summary>
        public double Sample(double x, double y)
        {
            // ── 第 2 步：定位格子 ──
            int xi = FloorToInt(x);
            int yi = FloorToInt(y);
            double xf = x - xi;          // 采样点在本格内的位置（0~1）
            double yf = y - yi;

            // 格子四角在置换表里的下标
            int aa = _perm[(xi & 255) + _perm[yi & 255]];
            int ab = _perm[(xi & 255) + _perm[(yi + 1) & 255]];
            int ba = _perm[((xi + 1) & 255) + _perm[yi & 255]];
            int bb = _perm[((xi + 1) & 255) + _perm[(yi + 1) & 255]];

            // ── 第 3 步：四角点积 ──
            // 左下角：偏移是 (xf, yf)；右下角：偏移是 (xf-1, yf)；左上： (xf, yf-1)；右上： (xf-1, yf-1)
            double d00 = Gradient(aa, xf, yf);
            double d10 = Gradient(ba, xf - 1, yf);
            double d01 = Gradient(ab, xf, yf - 1);
            double d11 = Gradient(bb, xf - 1, yf - 1);

            // ── 第 4 步：两条边各自混合，再混合成结果 ──
            double u = Fade(xf);
            double v = Fade(yf);

            return Lerp(v,
                        Lerp(u, d00, d10),   // 底边：x 方向混合
                        Lerp(u, d01, d11));  // 顶边：x 方向混合
        }

        /// <summary>
        /// 采样并映射到 **0~1**。`frequency` 是缩放：越大越"碎"，越小越"成片"。
        /// </summary>
        public double Sample01(double x, double y, double frequency = 1.0)
        {
            double v = Sample(x * frequency, y * frequency);

            // 理论范围可能略微越界 ±1，先夹再用 (v+1)/2 归一
            if (v < -1) v = -1;
            if (v > 1) v = 1;
            return (v + 1) * 0.5;
        }

        /// <summary>fade：`6t⁵ - 15t⁴ + 10t³`，让格与格之间平滑接上（见类注释第 4 步）。</summary>
        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

        private static double Lerp(double t, double a, double b) => a + t * (b - a);

        /// <summary>往负无穷取整（`(int)` 是向 0 取整，负数会错一格）。</summary>
        private static int FloorToInt(double value)
        {
            int i = (int)value;
            return value < i ? i - 1 : i;
        }

        /// <summary>
        /// 角点梯度：把一个 0~255 的哈希拆成 8 个方向之一（4 条轴 + 4 条对角），
        /// 再与偏移做点积。用 8 个固定方向而不是算出真实角度，是为了快且结果可复现。
        /// </summary>
        private static double Gradient(int hash, double x, double y)
        {
            return (hash & 7) switch
            {
                0 => x + y,
                1 => -x + y,
                2 => x - y,
                3 => -x - y,
                4 => x,
                5 => -x,
                6 => y,
                _ => -y,
            };
        }
    }
}
