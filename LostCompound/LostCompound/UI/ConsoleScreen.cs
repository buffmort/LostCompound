namespace LostCompound.UI
{
    /// <summary>
    /// 控制台分区布局。屏幕被切成三块，各自固定位置、互不干扰：
    ///
    ///   第 1 .. MapRows 行          地图区（固定，只在变化时重画）
    ///   紧接其后 StatusRows 行       状态区（血量 / 防御 / 水量）
    ///   剩下的行                     日志区（滚动，战斗日志都进这里）
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
        private int _width = 120;
        private string _lastMap;                   // 上次画过的地图文本（跳过无变化的重绘）
        private string _lastStatus;                // 上次画过的状态文本

        /// <summary>地图区行数（等于地图文本的行数）。</summary>
        public int MapRows { get; }

        /// <summary>状态区行数。</summary>
        public int StatusRows { get; }

        /// <summary>日志区行数。</summary>
        public int LogRows { get; }

        /// <summary>状态区起始行（1-based）。</summary>
        private int StatusTop => MapRows + 1;

        /// <summary>日志区起始行（1-based）。</summary>
        private int LogTop => MapRows + StatusRows + 1;

        public ConsoleScreen(int mapRows, int statusRows = 3)
        {
            _raw = Console.Out;                    // ← 关键：先抓真实输出
            _writer = new LogRegionWriter(this);

            MapRows = mapRows;
            StatusRows = statusRows;

            try { _width = Math.Max(Console.WindowWidth, 40); } catch { _width = 120; }

            int height;
            try { height = Console.WindowHeight; } catch { height = 40; }
            LogRows = Math.Max(3, height - LogTop);
        }

        /// <summary>接管 Console.Out，并清一次屏。</summary>
        public void Install()
        {
            _raw.Write("\u001b[2J");
            Console.SetOut(_writer);
        }

        /// <summary>把 Console.Out 还给真实输出（离开地图时调用）。</summary>
        public void Uninstall()
        {
            Console.SetOut(_raw);
        }

        /// <summary>重画地图区。内容与上次相同时直接跳过，避免无谓重绘导致闪烁。</summary>
        public void DrawMap(string mapText)
        {
            // 注意：C# 的 string 用 == 比较的是「内容」，Java 的 == 比较的是「引用」。
            // 所以这里可以直接写 ==，不需要像 Java 那样写 .equals()。
            if (mapText == _lastMap) return;
            _lastMap = mapText;

            string[] lines = mapText.Replace("\r", "").Split('\n');
            for (int i = 0; i < MapRows; i++)
            {
                WriteAt(i + 1, i < lines.Length ? lines[i] : "");
            }
        }

        /// <summary>重画状态区。statusText 里用 \n 分行。内容未变时跳过。</summary>
        public void DrawStatus(string statusText)
        {
            if (statusText == _lastStatus) return;
            _lastStatus = statusText;

            string[] lines = statusText.Replace("\r", "").Split('\n');
            for (int i = 0; i < StatusRows; i++)
            {
                WriteAt(StatusTop + i, i < lines.Length ? lines[i] : "");
            }
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

            int row = LogTop + _log.Count;
            if (row >= LogTop + LogRows) return;

            WriteAt(row, text);
            _raw.Write($"\u001b[{row};{text.Length + 1}H");   // 光标停在文字末尾，输入回显才正确
        }

        private void RedrawLog()
        {
            for (int i = 0; i < LogRows; i++)
            {
                string text;
                if (i < _log.Count)
                {
                    text = _log[i];
                }
                else if (i == _log.Count)
                {
                    text = _partial;              // 未完成行紧跟在已提交日志之后
                }
                else
                {
                    text = "";
                }
                WriteAt(LogTop + i, text);
            }

            // 光标放到「该输入的地方」：未完成行末尾
            int cursorRow = Math.Min(LogTop + _log.Count, LogTop + LogRows - 1);
            _raw.Write($"\u001b[{cursorRow};1H");
            if (_log.Count < LogRows)
            {
                // 让光标落在未完成行文字的后面，输入时回显位置正确
                _raw.Write($"\u001b[{cursorRow};{_partial.Length + 1}H");
            }
        }

        /// <summary>移动到指定行、清掉整行、再写入内容。</summary>
        private void WriteAt(int row, string text)
        {
            _raw.Write($"\u001b[{row};1H");   // 移到行首
            _raw.Write(text);
            _raw.Write("\u001b[K");           // 清掉这一行剩余部分
        }
    }
}
