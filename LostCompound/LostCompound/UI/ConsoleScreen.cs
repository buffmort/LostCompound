namespace LostCompound.UI
{
    /// <summary>
    /// 控制台左右分栏布局（带边框 + 独立输入框）。
    ///
    ///   左列（窄）                      右列（地图，宽而方）
    ///   ┌─ 状态 ──────────┐            ┌─ 地图 ──────────────────┐
    ///   │ 当前位置: (0, 0) │            │ +---+---+---+---+---+    │
    ///   │ 当前房间:Start   │            │ | S | F | E |   |   |    │
    ///   │ 水量:191/200     │            │ +---+---+---+---+---+    │
    ///   └─────────────────┘            │ ...                      │
    ///   ┌─ 日志 ──────────┐            │                          │
    ///   │ ...（滚动）      │            │                          │
    ///   │ ...              │            └──────────────────────────┘
    ///   └─────────────────┘
    ///   ┌─ 输入 ──────────┐
    ///   │ > _              │
    ///   └─────────────────┘
    ///
    /// 为什么左右分栏？地图是宽而方的（10×10 地图 = 41 列 × 21 行），
    /// 状态/日志/输入本来就窄。上下堆叠时地图会吃掉纵向空间，日志被压到个位数；
    /// 分栏之后，左列整列高度都归日志，日志能多好几倍。
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

        /// <summary>输入框里提示文字前面的前缀。</summary>
        private const string PromptPrefix = "> ";

        // ---- 尺寸 ----
        /// <summary>地图区内容行数。</summary>
        public int MapRows { get; }
        /// <summary>状态区内容行数。会按内容自适应，所以可写。</summary>
        public int StatusRows { get; private set; }
        /// <summary>日志区内容行数。状态框变高时日志让出对应行数，所以可写。</summary>
        public int LogRows { get; private set; }
        /// <summary>地图框外框宽度（含左右竖线）。会随地图内容宽度只涨不缩。</summary>
        public int MapWidth { get; private set; }
        /// <summary>左列各框外框宽度（含左右竖线）。随地图宽度反向变化。</summary>
        public int LeftWidth { get; private set; }

        /// <summary>终端宽度。重排时要用它重新分配左右列。</summary>
        private readonly int _terminalWidth;

        /// <summary>是否已经按「实际地图内容宽度」定过一次尺寸（首次绘制时允许收缩）。</summary>
        private bool _mapWidthReady;

        /// <summary>日志区最少保留的行数（状态框最多涨到把日志压到这里为止）。</summary>
        private const int MinimumLogRows = 3;

        /// <summary>
        /// 地图区最少保留的内容宽度（列）。约等于 4-5 格地图。
        /// 太小的话，迷雾下只探索了左上角时地图框会缩到只能显示 1 列，没法规划路线。
        /// </summary>
        private const int MinimumMapContentWidth = 21;

        /// <summary>终端高度，用于保证重排后总高依然放得下。</summary>
        private readonly int _terminalHeight;

        // ---- 位置（1-based）----
        private const int LeftBoxLeft = 1;
        private int MapBoxLeft => LeftBoxLeft + LeftWidth + 1;   // 中间留 1 列空隙

        private int StatusBoxTop => 1;
        private int LogBoxTop => StatusBoxTop + StatusRows + 2;
        private int InputBoxTop => LogBoxTop + LogRows + 2;

        private const int MapBoxTop = 1;

        /// <summary>整个布局占用的总行数（用于判断/恢复光标位置）。</summary>
        public int TotalRows => Math.Max(MapBoxTop + MapRows + 1, InputBoxTop + 2);

        /// <summary>竖线之后、内容之前的边距（"│ " 两格）。</summary>
        private const int ContentOffset = 2;

        public ConsoleScreen(int mapRows, int mapColumns, int statusRows = 3)
        {
            _raw = Console.Out;                    // ← 关键：先抓真实输出
            _writer = new LogRegionWriter(this);

            StatusRows = statusRows;

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
            // 左列总高 = 状态框(StatusRows+2) + 日志框(LogRows+2) + 输入框(3)
            // 右列总高 = 地图框(MapRows+2)
            // 整体不能超过终端高度，否则会触发终端滚动、绝对定位全废。
            _terminalHeight = terminalHeight;

            int leftOverhead = StatusRows + 7;                     // 状态框 + 输入框的固定部分
            int logRows = Math.Max(MinimumLogRows, terminalHeight - leftOverhead);

            int mapRowsFit = Math.Max(5, terminalHeight - 2);      // 地图框要上下边框各一行
            MapRows = Math.Min(mapRows, mapRowsFit);

            // 地图如果比左列矮，日志就受左列限制（两者取小，保证都在屏幕内）
            LogRows = logRows;
        }

        /// <summary>接管 Console.Out，清屏并画出日志框与输入框的固定边框。</summary>
        public void Install()
        {
            _raw.Write("\u001b[2J");
            Console.SetOut(_writer);

            // 日志框与输入框的边框只画一次（内容会变，边框不动）。
            // 地图框与状态框由 DrawMap / DrawStatus 各自绘制，因为它们的内容会变。
            DrawBoxTop(LogBoxTop, LeftBoxLeft, LeftWidth, "日志");
            DrawBoxBottom(LogBoxTop + LogRows + 1, LeftBoxLeft, LeftWidth);

            DrawBoxTop(InputBoxTop, LeftBoxLeft, LeftWidth, "输入");
            DrawBoxBottom(InputBoxTop + 2, LeftBoxLeft, LeftWidth);
            DrawBoxContent(InputBoxTop + 1, LeftBoxLeft, LeftWidth, "");
        }

        /// <summary>把 Console.Out 还给真实输出（离开地图时调用）。</summary>
        public void Uninstall()
        {
            // 把光标移到整个布局下方，否则退出地图后主菜单的输出会盖在框上面
            _raw.Write($"\u001b[{TotalRows + 1};1H");
            Console.SetOut(_raw);
        }

        /// <summary>重画地图区（右列）。内容与上次相同时直接跳过，避免无谓重绘导致闪烁。</summary>
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

                    // 宽度变化会平移所有框的水平位置，整屏重画一次，避免留下旧边框字符
                    _raw.Write("\u001b[2J");
                    RelayoutLeftColumn();
                }
            }

            DrawBox(MapBoxTop, MapBoxLeft, MapWidth, "地图", MapRows, lines);

            // 画地图会把光标留在右下角，这里放回输入框，玩家敲的字才会回显在框里
            RedrawInputBox();
        }

        /// <summary>
        /// 按当前 LeftWidth 重排左列（状态框、日志框、输入框的边框与内容）。
        /// 各框位置都是计算属性，所以只要重画一遍即可。
        /// </summary>
        private void RelayoutLeftColumn()
        {
            DrawBox(StatusBoxTop, LeftBoxLeft, LeftWidth, "状态", StatusRows, SplitLines(_lastStatus ?? ""));
            RedrawLogFrame();
            RedrawInputFrame();
            RedrawLog();
            RedrawInputBox();
        }

        /// <summary>
        /// 重画状态区（左列顶部）。statusText 里用 \n 分行。
        /// 行数变化时状态框会自动增高/变矮，多占的行由日志区让出，左列总高保持不变。
        /// </summary>
        public void DrawStatus(string statusText)
        {
            if (statusText == _lastStatus) return;
            _lastStatus = statusText;

            string[] lines = SplitLines(statusText);

            // 状态框按内容自适应：内容多就长高，少就变矮（至少 1 行）
            int wantedRows = Math.Max(1, lines.Length);
            if (wantedRows != StatusRows)
            {
                ResizeStatus(wantedRows);
            }

            DrawBox(StatusBoxTop, LeftBoxLeft, LeftWidth, "状态", StatusRows, lines);
        }

        /// <summary>
        /// 改变状态框高度：左列总高不变，多占的行由日志区让出。
        /// 各框位置都是计算属性（依赖 StatusRows），所以日志框/输入框会自己跟着挪，
        /// 这里只需要把它们的边框和内容重画一遍。
        /// </summary>
        private void ResizeStatus(int newRows)
        {
            int delta = newRows - StatusRows;
            int targetLog = LogRows - delta;

            if (targetLog < MinimumLogRows)
            {
                // 日志已压到下限，状态框不能再涨
                newRows -= MinimumLogRows - targetLog;
                if (newRows == StatusRows) return;
                delta = newRows - StatusRows;
            }

            StatusRows = newRows;
            LogRows -= delta;

            // 框的位置变了，边框与内容都要重画
            RedrawLogFrame();
            RedrawInputFrame();
            RedrawLog();
            RedrawInputBox();
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

            // 光标跑到提示文字之后，这样玩家敲的字回显在输入框里
            int col = LeftBoxLeft + ContentOffset + DisplayWidth(Clamp(PromptPrefix + _partial, LeftWidth));
            _raw.Write($"\u001b[{row};{col}H");
        }

        // ==================== 画框 ====================

        private static string[] SplitLines(string text) => text.Replace("\r", "").Split('\n');

        /// <summary>画一个带标题的完整框。</summary>
        private void DrawBox(int topRow, int leftCol, int width, string title, int contentRows, string[] content)
        {
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

        /// <summary>画一行框内内容：│ 文本…（补齐空格）│</summary>
        private void DrawBoxContent(int row, int col, int width, string text)
        {
            string clamped = Clamp(text, width);
            int inner = width - 2 - ContentOffset;                 // 可用内容宽度
            int pad = Math.Max(0, inner - DisplayWidth(clamped));

            WriteRawAt(row, col, "│" + new string(' ', ContentOffset - 1) + clamped + new string(' ', pad) + "│");
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
