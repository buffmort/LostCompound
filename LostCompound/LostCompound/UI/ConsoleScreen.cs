namespace LostCompound.UI
{
    /// <summary>
    /// 控制台左右分栏布局（带边框 + 独立输入框）。
    ///
    ///   左列（窄）                      右列（地图，宽而方）
    ///   ┌─ 敌人 ──────────┐            ┌─ 地图 ──────────────────┐
    ///   │ [0] 僵尸(30.0)   │            │ +---+---+---+---+---+    │
    ///   │ [1] 岩浆怪(80.0) │            │ | S | F | E |   |   |    │
    ///   │ [2] （空）       │            │ +---+---+---+---+---+    │
    ///   └─────────────────┘            │ ...                      │
    ///   ┌─ 日志 ──────────┐            └──────────────────────────┘
    ///   │ ...（滚动）      │            ┌─ 状态 ──────────────────┐
    ///   │ ...              │            │ 当前房间:Start           │
    ///   └─────────────────┘            │ 血量:.... 防御:.... 护盾: |
    ///   ┌─ 输入 ──────────┐            │ 力量:....                │
    ///   │ > _              │            │ 熔岩瀑布水量：200/200    │
    ///   └─────────────────┘            │ （DescribeStatus 另有行  │
    ///                                  │   时会顶掉上面某一行）   │
    ///                                  └──────────────────────────┘
    ///                                  ┌─ Buff ──────────────────┐
    ///                                  │ 流血: 每回合7伤害 · 剩余2回合 │
    ///                                  │ （空）                   │
    ///                                  └──────────────────────────┘
    ///
    /// 状态框是 <see cref="StatusRows"/>（4）行，上面的示意图就是它的实际高度 ——
    /// 注意它**并不**只有「当前房间 + 环境」两行：战斗时玩家自己的血量/防御/护盾/力量
    /// 会插在房间行之后（见 Player.RefreshStatus），超出的行会被 DrawBox 丢掉。
    ///
    /// 为什么左右分栏？地图是宽而方的（10×10 地图 = 41 列 × 21 行），
    /// 状态/日志/输入本来就窄。上下堆叠时地图会吃掉纵向空间，日志被压到个位数；
    /// 分栏之后，左列整列高度都归日志，日志能多好几倍。
    ///
    /// 为什么敌人框在左列顶部？
    ///   「敌人」和「状态」原先都在左列顶部，只能有一个占着。敌人列表是战斗时每次决策
    ///   都要看的信息，而且 Attack / SelectTarget / OpenItems 的提示都写着「选择编号」——
    ///   编号必须一直挂在眼前。所以把敌人框固定钉在左列顶部（恒定 3 行），
    ///   状态框挪到地图框下方：状态只在「进新房间 / 受伤」时变，不需要头部位置。
    ///
    /// 光标定位用 ANSI 转义序列（\u001b[行;列H），而不是 Console.SetCursorPosition。
    /// 原因：ANSI 在「输出被重定向」时只是失效，不会抛异常；SetCursorPosition 会直接抛 IOException。
    ///
    /// 注意：所有画屏幕的操作都必须走 <see cref="_raw"/>（重定向之前的真实输出），
    /// 否则会写回自己的重定向器，造成无限递归。
    /// </summary>
    public sealed class ConsoleScreen
    {
        private readonly TextWriter _raw;          // 真实输出，必须在 SetOut 之前抓住
        private readonly LogRegionWriter _writer;
        private readonly List<string> _log = [];
        private string _partial = "";              // Console.Write 留下的未换行内容 = 当前的输入提示
        private string _lastMap;                   // 上次画过的地图文本（跳过无变化的重绘）
        private string _lastStatus;                // 上次画过的状态文本
        private string _lastEnemies = "";          // 上次画过的敌人文本
        private string _lastBuffs = "";            // 上次画过的 Buff 文本

        /// <summary>输入框里提示文字前面的前缀。</summary>
        private const string PromptPrefix = "> ";

        // ---- 尺寸 ----
        /// <summary>地图区内容行数。</summary>
        public int MapRows { get; }
        /// <summary>
        /// 状态区内容行数。固定 4 行 —— 不再按内容自适应（自适应会让日志跟着抖动）。
        ///
        /// ⚠️ 这个数字决定了状态栏「最多能显示几行」：
        /// 超出它的行会被 DrawBox 静默丢弃（不报错、不换行），
        /// 所以往 RefreshStatus 里加新行时，必须同时确认这里够用。
        /// 每加 1 行，右列总高 +1，多出来的那行会从地图扣（见构造函数的纵向分配）。
        /// </summary>
        public const int StatusRows = 4;
        /// <summary>日志区内容行数。两列分完剩下的纵向空间全归它，所以可写。</summary>
        public int LogRows { get; private set; }
        /// <summary>敌人框的内容行数。固定 3 行 —— 足以放下三个敌人。</summary>
        public const int EnemyRows = 3;
        /// <summary>
        /// Buff 框的内容行数。固定 2 行 —— 够放满当前全部 buff 种类（现在只有流血一种）。
        /// 为什么取 2 而不是任务书建议的 3：右列纵向已经排了「地图 + 状态 + Buff」三个框，
        /// 每多 1 行都要从地图上扣（见构造函数里的纵向分配），2 行足以容纳现存 buff 种类，
        /// 把省下的 1 行留给地图。将来 buff 种类超过 2 种时再改回 3 即可（只改这一个常量）。
        /// </summary>
        public const int BuffRows = 2;
        /// <summary>地图框外框宽度（含左右竖线）。会随地图内容宽度只涨不缩。</summary>
        public int MapWidth { get; private set; }
        /// <summary>左列各框外框宽度（含左右竖线）。随地图宽度反向变化。</summary>
        public int LeftWidth { get; private set; }

        /// <summary>终端宽度。重排时要用它重新分配左右列。</summary>
        private readonly int _terminalWidth;

        /// <summary>是否已经按「实际地图内容宽度」定过一次尺寸（首次绘制时允许收缩）。</summary>
        private bool _mapWidthReady;

        /// <summary>
        /// 地图区最少保留的内容宽度（列）。41 列 ≈ 10 格地图（每格 4 字符 + 边框）。
        ///
        /// 为什么取整张地图的宽度而不是"够看几格就行"：
        ///   状态框和 Buff 框的宽度**绑在地图框上**（都在右列、用同一个 MapWidth），
        ///   而地图框会随探索进度只涨不缩。如果最小值太小，迷雾下只探索了左上角时
        ///   地图框会很窄 → 状态框跟着窄 → 「熔岩瀑布水量：191/200」这类描述被截断。
        ///   取满宽度后，状态栏从第一屏起就够用，不必等探索开。
        /// 代价：早期地图框右侧会留一段空白（迷雾未探索的区域本来也没内容）。
        /// </summary>
        private const int MinimumMapContentWidth = 41;

        /// <summary>终端高度，用于保证重排后总高依然放得下。</summary>
        private readonly int _terminalHeight;

        // ---- 位置（1-based）----
        private const int LeftBoxLeft = 1;
        private int MapBoxLeft => LeftBoxLeft + LeftWidth + 1;   // 中间留 1 列空隙

        // 左列：敌人（固定 3 行内容）→ 日志 → 输入
        private int EnemyBoxTop => 1;
        private int LogBoxTop => EnemyBoxTop + EnemyRows + 2;
        private int InputBoxTop => LogBoxTop + LogRows + 2;

        // 右列：地图 → 状态 → Buff
        private const int MapBoxTop = 1;
        private int StatusBoxTop => MapBoxTop + MapRows + 2;
        /// <summary>Buff 框顶部行号：状态框正下方（中间不加空隙，两框紧贴成一组）。</summary>
        private int BuffBoxTop => StatusBoxTop + StatusRows + 2;

        /// <summary>
        /// 整个布局占用的总行数（用于判断/恢复光标位置）。
        /// 左右两列各自占满纵向空间，所以取两者的最大值 —— 少算了矮的那列没关系，
        /// 但绝不能少算高的那列，否则 Uninstall 后光标会停在框中间。
        /// </summary>
        public int TotalRows => Math.Max(InputBoxTop + 2, BuffBoxTop + BuffRows + 2);

        /// <summary>竖线之后、内容之前的边距（"│ " 两格）。</summary>
        private const int ContentOffset = 2;

        public ConsoleScreen(int mapRows, int mapColumns)
        {
            _raw = Console.Out;                    // ← 关键：先抓真实输出
            _writer = new LogRegionWriter(this);

            int terminalWidth;
            int terminalHeight;
            try { terminalWidth = Console.WindowWidth; } catch { terminalWidth = 100; }
            try { terminalHeight = Console.WindowHeight; } catch { terminalHeight = 40; }

            _terminalWidth = terminalWidth;

            // ---- 横向分配 ----
            // 地图框要放得下地图（内容宽 + 左右边距 + 两条竖线）
            int wantedMapWidth = mapColumns + 2 * ContentOffset + 2;
            // 左列给个合适的固定宽度：要装得下「熔岩瀑布水量：191/200」这类状态行
            int wantedLeftWidth = 60;

            int budget = terminalWidth - 1;                       // 留最右一列不写，避免自动换行
            if (wantedMapWidth + 1 + wantedLeftWidth > budget)
            {
                // 太窄：先压左列（日志会被截断），还不够再压地图
                int excess = wantedMapWidth + 1 + wantedLeftWidth - budget;
                int takeFromLeft = Math.Min(excess, Math.Max(0, wantedLeftWidth - 26));
                wantedLeftWidth -= takeFromLeft;
                excess -= takeFromLeft;
                wantedMapWidth = Math.Max(20, wantedMapWidth - excess);
            }

            MapWidth = wantedMapWidth;
            LeftWidth = wantedLeftWidth;

            // ---- 纵向分配 ----
            // 两列各自的总高（含边框）：
            //   左列 = 敌人框(EnemyRows + 2) + 日志框(LogRows + 2) + 输入框(3)
            //        = EnemyRows + LogRows + 7 = LogRows + 10
            //   右列 = 地图框(MapRows + 2) + 状态框(StatusRows + 2) + Buff框(BuffRows + 2)
            //        = MapRows + StatusRows + BuffRows + 6 = MapRows + 12
            // 两者取最大不能超过终端高度，否则会触发终端滚动、绝对定位全废（见 CONVENTIONS 2.6）。
            _terminalHeight = terminalHeight;

            // 右列超高时优先压缩地图（日志比地图多显示一行 ≠ 少看一格地图，地图压到 5 行就够辨认方向）
            // 反推：MapRows ≤ terminalHeight - StatusRows - BuffRows - 6
            int maxMapRows = terminalHeight - StatusRows - BuffRows - 6;
            MapRows = Math.Clamp(mapRows, 5, Math.Max(5, maxMapRows));

            // 左列超高时压缩日志：已知「左列总高 = LogRows + 10」，
            // 令其 ≤ terminalHeight 即得 LogRows ≤ terminalHeight - 10。
            // 反推一遍：敌人框上下边框(2) + 输入框(3) + 日志框上下边框(2) 共 7 行是固定开销，
            // 终端高度减去这 7 行就是日志内容能占的行数。
            const int LeftColumnFixedHeight = 2 + 3 + 2;           // 敌人框边框 + 输入框 + 日志框边框
            int logRowsFit = terminalHeight - LeftColumnFixedHeight - EnemyRows;
            LogRows = Math.Max(1, logRowsFit);

            // 极端矮屏兜底：两列各有绕不开的硬下限 ——
            //   左列 ≥ 敌人框(3+2) + 日志框(1+2) + 输入框(3) = 11 行
            //   右列 ≥ 地图框(3+2) + 状态框(4+2) + Buff框(2+2) = 13 行
            // 终端矮到 11 行以下时，任何排布都放不下；此时尽量贴近上限。
            // 注意这里仍然只压地图：Buff 框是这一轮新增的常驻框，压它等于把功能压没了。
            int overRight = (MapRows + StatusRows + BuffRows + 6) - terminalHeight;
            if (overRight > 0)
            {
                MapRows = Math.Max(3, MapRows - overRight);
            }

            // 左列压完（理论上已经被 logRowsFit 保证不超；这里只做最后一道防线）
            int overLeft = (EnemyRows + LogRows + 7) - terminalHeight;
            if (overLeft > 0)
            {
                MapRows = Math.Max(3, MapRows - overLeft);
            }
        }

        /// <summary>
        /// 接管 Console.Out，清屏并画出四个固定框的边框。
        /// 敌人框与状态框的内容每回合都会变，但边框一次画好即可（内容由 DrawEnemies / DrawStatus 填）。
        /// </summary>
        public void Install()
        {
            _raw.Write("\u001b[2J");
            Console.SetOut(_writer);

            // 日志框与输入框的边框只画一次（内容会变，边框不动）。
            DrawBoxTop(LogBoxTop, LeftBoxLeft, LeftWidth, "日志");
            DrawBoxBottom(LogBoxTop + LogRows + 1, LeftBoxLeft, LeftWidth);

            DrawBoxTop(InputBoxTop, LeftBoxLeft, LeftWidth, "输入");
            DrawBoxBottom(InputBoxTop + 2, LeftBoxLeft, LeftWidth);
            DrawBoxContent(InputBoxTop + 1, LeftBoxLeft, LeftWidth, "");

            // 敌人框（左列顶部）：固定 3 行内容，先把空框画出来
            DrawBox(EnemyBoxTop, LeftBoxLeft, LeftWidth, "敌人", EnemyRows, SplitLines(""));

            // 状态框（右列、地图下方）：宽度跟着 MapWidth 走，所以必须等地图宽度定下来后再画。
            // 首次绘制时 DrawMap 可能还会调整 MapWidth 并整屏重排，那时会连同状态框一起重画。
            DrawBox(StatusBoxTop, MapBoxLeft, MapWidth, "状态", StatusRows, SplitLines(_lastStatus ?? ""));

            // Buff 框（右列、状态框正下方）：与状态框同理，宽度绑在 MapWidth 上，位置绑在 MapRows 上。
            DrawBox(BuffBoxTop, MapBoxLeft, MapWidth, "Buff", BuffRows, SplitLines(_lastBuffs ?? ""));
        }

        /// <summary>把 Console.Out 还给真实输出（离开地图时调用）。</summary>
        public void Uninstall()
        {
            // 把光标移到整个布局下方，否则退出地图后主菜单的输出会盖在框上面
            _raw.Write($"\u001b[{TotalRows + 1};1H");
            Console.SetOut(_raw);
        }

        /// <summary>重画地图区（右列上半）。内容与上次相同时直接跳过，避免无谓重绘导致闪烁。</summary>
        public void DrawMap(string mapText)
        {
            // 注意：C# 的 string 用 == 比较的是「内容」，Java 的 == 比较的是「引用」。
            // 所以这里可以直接写 ==，不需要像 Java 那样写 .equals()。
            if (mapText == _lastMap) return;
            _lastMap = mapText;

            string[] lines = SplitLines(mapText);

            // 实际内容宽度：去掉行尾空格后按显示宽度算（CJK 双宽）。
            // 早期迷雾下只有左上角一小块有内容，TrimEnd 之后行都很短，框就该窄。
            int contentWidth = 0;
            foreach (string line in lines)
            {
                int w = DisplayWidth(line.TrimEnd());
                if (w > contentWidth) contentWidth = w;
            }

            int wanted = contentWidth + 2 * ContentOffset + 2;

            // 地图框最小宽度：内容至少留 21 列（约 4-5 格地图）。
            // 否则迷雾下只探索了左上角时，框会缩到只显示 1 列地图，根本没法规划路线。
            int minimum = MinimumMapContentWidth + 2 * ContentOffset + 2;
            if (wanted < minimum) wanted = minimum;

            // 窄终端下给左列留出最小宽度，避免地图把屏幕撑破导致自动换行
            int maxWanted = Math.Max(20, _terminalWidth - 2 - 26);
            if (wanted > maxWanted) wanted = maxWanted;

            // 只涨不缩：探索时地图只会变宽，缩回去会让布局来回抖动。
            // 例外是第一次绘制 —— 构造函数里的宽度是按「未 TrimEnd 的地图文本」估的，通常偏大。
            if (!_mapWidthReady || wanted > MapWidth)
            {
                _mapWidthReady = true;
                if (wanted != MapWidth)
                {
                    MapWidth = wanted;
                    LeftWidth = Math.Clamp(_terminalWidth - 2 - MapWidth, 26, 60);

                    // 宽度变化会平移所有框的水平位置（状态框跟着 MapWidth 走），
                    // 整屏重画一次，避免留下旧边框字符
                    _raw.Write("\u001b[2J");
                    Relayout();
                }
            }

            DrawBox(MapBoxTop, MapBoxLeft, MapWidth, "地图", MapRows, lines);

            // 画地图会把光标留在右下角，这里放回输入框，玩家敲的字才会回显在框里
            RedrawInputBox();
        }

        /// <summary>
        /// 按当前 LeftWidth / MapWidth 重排两列（所有框的边框与内容）。
        /// 各框位置都是计算属性，所以只要重画一遍即可。
        /// 注意必须重排「两列」：状态框的宽度绑在 MapWidth 上，地图宽度一变它也跟着变。
        /// </summary>
        private void Relayout()
        {
            DrawBox(EnemyBoxTop, LeftBoxLeft, LeftWidth, "敌人", EnemyRows, SplitLines(_lastEnemies));
            RedrawLogFrame();
            RedrawInputFrame();
            RedrawLog();
            RedrawInputBox();

            DrawBox(MapBoxTop, MapBoxLeft, MapWidth, "地图", MapRows, SplitLines(_lastMap ?? ""));
            DrawBox(StatusBoxTop, MapBoxLeft, MapWidth, "状态", StatusRows, SplitLines(_lastStatus ?? ""));
            // Buff 框的宽度同样绑在 MapWidth 上，地图一变宽它也要跟着重画
            DrawBox(BuffBoxTop, MapBoxLeft, MapWidth, "Buff", BuffRows, SplitLines(_lastBuffs ?? ""));
        }

        /// <summary>
        /// 重画敌人区（左列顶部）。enemyText 里用 \n 分行，最多 3 行（EnemyRows）。
        /// 不足的行会自动补空，超过的行由 DrawBox 忽略。
        /// </summary>
        public void DrawEnemies(string enemyText)
        {
            if (enemyText == _lastEnemies) return;
            _lastEnemies = enemyText;

            // 整个框重画一遍（含标题栏）：DrawBox 会按 EnemyRows 把不足的行补空、
            // 把多余的行丢掉，正好满足「固定 3 行、不足补空」的语义，不需要单独维护边框。
            DrawBox(EnemyBoxTop, LeftBoxLeft, LeftWidth, "敌人", EnemyRows, SplitLines(enemyText));
        }

        /// <summary>
        /// 重画状态区（右列、地图框正下方）。statusText 里用 \n 分行。
        /// 行数固定 <see cref="StatusRows"/>（**4**）—— 原先的「按内容涨缩、从日志借行」会导致整列抖动，
        /// 而且状态框现在跟地图同宽，高度再变会连带右列总高变化，风险更大。
        /// （这份注释上一版写的是「固定 StatusRows（3）」，与常量的实际值 4 不符，已改对。）
        /// </summary>
        public void DrawStatus(string statusText)
        {
            if (statusText == _lastStatus) return;
            _lastStatus = statusText;

            DrawBox(StatusBoxTop, MapBoxLeft, MapWidth, "状态", StatusRows, SplitLines(statusText));
        }

        /// <summary>
        /// 重画 Buff 区（右列、状态框下方）。buffText 里用 \n 分行，最多 BuffRows 行。
        ///
        /// 为什么单独开一个框而不是塞进状态框：状态框是「玩家自己 + 环境」的固定快照
        /// （位置/房间/血迹/水量），行数被 StatusRows 钉死；buff 是数量可变的列表。
        /// 两者混在一起，buff 一来一回就会把状态行挤出去，行数又得自适应（会抖动）。
        ///
        /// 宽度同样用 MapWidth、起始列 MapBoxLeft —— 与状态框左右对齐，右列看起来是一整列。
        /// 空字符串 = 框内全空（没有 buff 时就是一块 2 行的空白框）。
        /// </summary>
        public void DrawBuffs(string buffText)
        {
            // 与 DrawStatus / DrawEnemies 同一口径：内容没变就不重画，避免每回合整框闪烁
            if (buffText == _lastBuffs) return;
            _lastBuffs = buffText;

            DrawBox(BuffBoxTop, MapBoxLeft, MapWidth, "Buff", BuffRows, SplitLines(buffText));
        }

        /// <summary>单独重画日志框的上下边框（位置变化时用）。</summary>
        private void RedrawLogFrame()
        {
            DrawBoxTop(LogBoxTop, LeftBoxLeft, LeftWidth, "日志");
            DrawBoxBottom(LogBoxTop + LogRows + 1, LeftBoxLeft, LeftWidth);
        }

        /// <summary>单独重画输入框的边框（位置变化时用）。</summary>
        private void RedrawInputFrame()
        {
            DrawBoxTop(InputBoxTop, LeftBoxLeft, LeftWidth, "输入");
            DrawBoxBottom(InputBoxTop + 2, LeftBoxLeft, LeftWidth);
        }

        /// <summary>提交一条日志（由 <see cref="LogRegionWriter"/> 在遇到换行时调用）。</summary>
        public void AppendLog(string line)
        {
            _partial = "";
            _log.Add(line);
            while (_log.Count > LogRows) _log.RemoveAt(0);
            RedrawLog();
            RedrawInputBox();
        }

        /// <summary>
        /// 更新「当前未完成行」（Console.Write 不带换行时）。
        /// 这类内容几乎都是输入提示（如 "请选择: "），所以放进输入框，而不是日志区。
        /// </summary>
        public void SetPartial(string text)
        {
            _partial = text;
            RedrawInputBox();
        }

        /// <summary>设置输入框里显示的提示文字（光标自动定位到末尾）。</summary>
        public void SetPrompt(string text)
        {
            _partial = text;
            RedrawInputBox();
        }

        private void RedrawLog()
        {
            for (int i = 0; i < LogRows; i++)
            {
                string text = i < _log.Count ? _log[i] : "";
                DrawBoxContent(LogBoxTop + 1 + i, LeftBoxLeft, LeftWidth, text);
            }
        }

        /// <summary>重画输入框内容，并把光标定位到待输入的位置。</summary>
        private void RedrawInputBox()
        {
            int row = InputBoxTop + 1;
            DrawBoxContent(row, LeftBoxLeft, LeftWidth, PromptPrefix + _partial);

            // 光标跑到提示文字之后，这样玩家敲的字回显在输入框里。
            // 列号 = 左边框（1 列）+ ContentOffset 格边距 + 提示文字宽度 —— 与 DrawBoxContent
            // 实际写出的文字位置**逐字对应**（那边也是 │ + ContentOffset 格 + 文字）。
            // 原先这里减 2、而 DrawBoxContent 只写 1 格边距时恰好蒙对；DrawBoxContent 补齐宽度后
            // 两者统一成 ContentOffset，光标不再错位到文字前面。
            int col = LeftBoxLeft + ContentOffset + DisplayWidth(Clamp(PromptPrefix + _partial, LeftWidth));
            _raw.Write($"\u001b[{row};{col}H");
        }

        // ==================== 画框 ====================

        private static string[] SplitLines(string text) => text.Replace("\r", "").Split('\n');

        /// <summary>画一个带标题的完整框。</summary>
        private void DrawBox(int topRow, int leftCol, int width, string title, int contentRows, string[] content)
        {
            // ⚠️ 防静默丢行：内容比框高时，多出来的行会被下面的循环直接忽略 ——
            // 不报错、不换行，从界面上看就是「我明明写了却没显示」。
            // 这个坑踩过两次（「力量」挤掉水量、测试行又挤掉一次），所以这里主动喊一声。
            // 只丢 Debug 断点式提示，不影响正常输出（正常情况各框的行数都是配平的）。
            if (content.Length > contentRows)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ConsoleScreen] 「{title}」框只有 {contentRows} 行，但收到 {content.Length} 行 —— " +
                    $"后 {content.Length - contentRows} 行会被丢弃。请调大对应的 *Rows 常量。");
            }

            DrawBoxTop(topRow, leftCol, width, title);

            for (int i = 0; i < contentRows; i++)
            {
                DrawBoxContent(topRow + 1 + i, leftCol, width, i < content.Length ? content[i] : "");
            }

            DrawBoxBottom(topRow + contentRows + 1, leftCol, width);
        }

        private void DrawBoxTop(int row, int col, int width, string title)
        {
            // ┌─ 标题 ───────────────┐
            string head = "┌─ " + title + " ";
            int fill = Math.Max(0, width - DisplayWidth(head) - 1);
            WriteRawAt(row, col, head + new string('─', fill) + "┐");
        }

        private void DrawBoxBottom(int row, int col, int width)
        {
            WriteRawAt(row, col, "└" + new string('─', Math.Max(0, width - 2)) + "┘");
        }

        /// <summary>
        /// 画一行框内内容：│ 文本…（补齐空格）│
        ///
        /// ⚠️ 整行的**显示宽度必须正好等于 <paramref name="width"/>**，与上下边框（<see cref="DrawBoxTop"/> /
        /// <see cref="DrawBoxBottom"/>，各为 width 列）逐列对齐 —— 否则右边框会比上下边框的 ┐ / ┘ 少一列，
        /// 整个框的右边缘出现锯齿。
        /// 原先这里写的是 <c>new string(' ', ContentOffset - 1)</c>（只有 1 格边距），
        /// 而宽度预算是按 ContentOffset（2 格）扣的，于是每行实宽 = width - 1，正好差这一列。
        /// 改成 ContentOffset 之后：1（│）+ 2（边距）+ clamped + pad + 1（│）= width。
        /// </summary>
        private void DrawBoxContent(int row, int col, int width, string text)
        {
            string clamped = Clamp(text, width);
            int inner = width - 2 - ContentOffset;                 // 可用内容宽度
            int pad = Math.Max(0, inner - DisplayWidth(clamped));

            WriteRawAt(row, col, "│" + new string(' ', ContentOffset) + clamped + new string(' ', pad) + "│");
        }

        /// <summary>按显示宽度截断，避免撑破右边框。</summary>
        private static string Clamp(string text, int boxWidth)
        {
            int limit = boxWidth - 2 - ContentOffset;
            if (limit <= 0) return "";

            int width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int w = IsWide(text[i]) ? 2 : 1;
                if (width + w > limit) return text[..i];
                width += w;
            }
            return text;
        }

        private void WriteRawAt(int row, int col, string text)
        {
            // 注意：这里绝不能用 "\u001b[K"（清到行尾）。
            // 左右分栏时，同一行右边还有另一个框，擦到行尾会把邻居的边框一起擦掉。
            // 各框自己都按「外框宽度」精确补足了空格，所以不需要清行尾。
            _raw.Write($"\u001b[{row};{col}H");
            _raw.Write(text);
        }

        // ==================== 显示宽度（CJK 双宽） ====================

        /// <summary>
        /// 计算字符串在终端里占几列。中文/全角字符算 2 列，其余算 1 列。
        /// 直接用 string.Length 会让带中文的行右边框对不齐。
        /// </summary>
        private static int DisplayWidth(string text)
        {
            int width = 0;
            foreach (char c in text)
            {
                width += IsWide(c) ? 2 : 1;
            }
            return width;
        }

        /// <summary>是否东亚宽字符（占两列）。覆盖常见区段。</summary>
        private static bool IsWide(char c) =>
            (c >= 0x1100 && c <= 0x115F) ||   // 韩文字母
            (c >= 0x2E80 && c <= 0xA4CF) ||   // CJK 部首 / 假名 / 汉字
            (c >= 0xAC00 && c <= 0xD7A3) ||   // 韩文音节
            (c >= 0xF900 && c <= 0xFAFF) ||   // CJK 兼容汉字
            (c >= 0xFE30 && c <= 0xFE6F) ||   // CJK 兼容形式
            (c >= 0xFF00 && c <= 0xFF60) ||   // 全角字符
            (c >= 0xFFE0 && c <= 0xFFE6);
    }
}
