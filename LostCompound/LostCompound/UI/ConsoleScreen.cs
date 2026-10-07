namespace LostCompound.UI
{
    /// <summary>
    /// 控制台分区布局（带边框）。屏幕被切成三块带框区域，各自固定位置、互不干扰：
    ///
    ///   ┌─ 地图 ─────────────┐
    ///   │ ...                │
    ///   └────────────────────┘
    ///
    ///   ┌─ 状态 ─────────────┐
    ///   │ ...                │
    ///   └────────────────────┘
    ///
    ///   ┌─ 日志 ─────────────┐
    ///   │ ...（滚动）         │
    ///   └────────────────────┘
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
        private string _partial = "";              // Console.Write 留下的未换行内容
        private string _lastMap;                   // 上次画过的地图文本（跳过无变化的重绘）
        private string _lastStatus;                // 上次画过的状态文本

        /// <summary>地图区内容行数（等于地图文本的行数）。</summary>
        public int MapRows { get; }

        /// <summary>状态区内容行数。</summary>
        public int StatusRows { get; }

        /// <summary>日志区内容行数。</summary>
        public int LogRows { get; }

        /// <summary>边框外框宽度（含左右竖线）。</summary>
        private int BoxWidth { get; }

        // 三个框的位置（1-based 行号）
        private int MapBoxTop => 1;
        private int StatusBoxTop => MapRows + 4;
        private int LogBoxTop => StatusBoxTop + StatusRows + 3;

        /// <summary>竖线之后、内容之前的边距（"│ " 两格）。</summary>
        private const int ContentOffset = 2;

        public ConsoleScreen(int mapRows, int mapColumns, int statusRows = 3)
        {
            _raw = Console.Out;                    // ← 关键：先抓真实输出
            _writer = new LogRegionWriter(this);

            MapRows = mapRows;
            StatusRows = statusRows;

            int terminalWidth;
            int terminalHeight;
            try { terminalWidth = Console.WindowWidth; } catch { terminalWidth = 100; }
            try { terminalHeight = Console.WindowHeight; } catch { terminalHeight = 40; }

            // 外框宽 = 地图内容宽度 + 左右边距(各 2) + 两条竖线，且不超过终端宽度
            int wanted = mapColumns + 2 * ContentOffset + 2;
            BoxWidth = Math.Max(24, Math.Min(wanted, terminalWidth - 1));

            LogRows = Math.Max(3, terminalHeight - LogBoxTop - 2);
        }

        /// <summary>接管 Console.Out，清屏并画出日志框的边框。</summary>
        public void Install()
        {
            _raw.Write("\u001b[2J");
            Console.SetOut(_writer);

            // 日志框的上下边框只画一次（框内内容会滚动，边框不动）。
            // 地图框与状态框由 DrawMap / DrawStatus 各自绘制，因为它们的内容会变。
            DrawBoxTop(LogBoxTop, "日志");
            DrawBoxBottom(LogBoxTop + LogRows + 1);
        }

        /// <summary>把 Console.Out 还给真实输出（离开地图时调用）。</summary>
        public void Uninstall()
        {
            // 把光标移到日志框下方，否则退出地图后主菜单的输出会盖在框上面
            _raw.Write($"\u001b[{LogBoxTop + LogRows + 2};1H");
            Console.SetOut(_raw);
        }

        /// <summary>重画地图区。内容与上次相同时直接跳过，避免无谓重绘导致闪烁。</summary>
        public void DrawMap(string mapText)
        {
            // 注意：C# 的 string 用 == 比较的是「内容」，Java 的 == 比较的是「引用」。
            // 所以这里可以直接写 ==，不需要像 Java 那样写 .equals()。
            if (mapText == _lastMap) return;
            _lastMap = mapText;

            DrawBox(MapBoxTop, "地图", MapRows, SplitLines(mapText));

            // 地图区重画会覆盖它上方的内容，但日志与状态在它下方，不受影响
        }

        /// <summary>重画状态区。statusText 里用 \n 分行。内容未变时跳过。</summary>
        public void DrawStatus(string statusText)
        {
            if (statusText == _lastStatus) return;
            _lastStatus = statusText;

            DrawBox(StatusBoxTop, "状态", StatusRows, SplitLines(statusText));
        }

        /// <summary>提交一条日志（由 <see cref="LogRegionWriter"/> 在遇到换行时调用）。</summary>
        public void AppendLog(string line)
        {
            _partial = "";
            _log.Add(line);
            while (_log.Count > LogRows) _log.RemoveAt(0);
            RedrawLog();
        }

        /// <summary>更新「当前未完成行」（Console.Write 不带换行时）。只重画这一行。</summary>
        public void SetPartial(string text)
        {
            _partial = text;

            int row = LogBoxTop + 1 + _log.Count;      // 日志内容从框顶的下一行开始
            if (row >= LogBoxTop + 1 + LogRows) return;

            DrawBoxContent(row, text);
            _raw.Write($"\u001b[{row};{ContentOffset + DisplayWidth(Clamp(text)) + 1}H");
        }

        private void RedrawLog()
        {
            for (int i = 0; i < LogRows; i++)
            {
                string text;
                if (i < _log.Count) text = _log[i];
                else if (i == _log.Count) text = _partial;
                else text = "";

                DrawBoxContent(LogBoxTop + 1 + i, text);
            }

            // 光标放到「该输入的地方」：未完成行末尾
            int cursorRow = LogBoxTop + 1 + Math.Min(_log.Count, LogRows - 1);
            int cursorCol = ContentOffset + DisplayWidth(Clamp(_partial)) + 1;
            _raw.Write($"\u001b[{cursorRow};{cursorCol}H");
        }

        // ==================== 画框 ====================

        private static string[] SplitLines(string text) => text.Replace("\r", "").Split('\n');

        /// <summary>画一个带标题的完整框。</summary>
        private void DrawBox(int topRow, string title, int contentRows, string[] content)
        {
            DrawBoxTop(topRow, title);

            for (int i = 0; i < contentRows; i++)
            {
                DrawBoxContent(topRow + 1 + i, i < content.Length ? content[i] : "");
            }

            DrawBoxBottom(topRow + contentRows + 1);
        }

        private void DrawBoxTop(int row, string title)
        {
            // ┌─ 标题 ───────────────┐
            string head = "┌─ " + title + " ";
            int fill = Math.Max(0, BoxWidth - DisplayWidth(head) - 1);
            WriteRawAt(row, head + new string('─', fill) + "┐");
        }

        private void DrawBoxBottom(int row)
        {
            WriteRawAt(row, "└" + new string('─', Math.Max(0, BoxWidth - 2)) + "┘");
        }

        /// <summary>画一行框内内容：│ 文本…（补齐空格）│</summary>
        private void DrawBoxContent(int row, string text)
        {
            string clamped = Clamp(text);
            int inner = BoxWidth - 2 - ContentOffset;                 // 可用内容宽度
            int pad = Math.Max(0, inner - DisplayWidth(clamped));

            WriteRawAt(row, "│" + new string(' ', ContentOffset - 1) + clamped + new string(' ', pad) + "│");
        }

        /// <summary>按显示宽度截断，避免撑破右边框。</summary>
        private string Clamp(string text)
        {
            int limit = BoxWidth - 2 - ContentOffset;
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

        private void WriteRawAt(int row, string text)
        {
            _raw.Write($"\u001b[{row};1H");
            _raw.Write(text);
            _raw.Write("\u001b[K");           // 清掉这一行剩余部分
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
