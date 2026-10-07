using System.Runtime.InteropServices;

namespace LostCompound.Tools
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public static class GameLauncher
    {
        // ---- Win32：开启虚拟终端（ANSI 转义）处理 ----
        // Windows 的传统控制台（conhost）默认**不解释** ANSI 转义序列，
        // 所以裸跑 exe 时 \u001b[41;1H 这类光标定位会被当成普通文字原样打印出来，
        // 满屏 "[41;1H│"。Rider 的 Run 窗口是它自己解析的，所以在 IDE 里看不出问题。
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        /// <summary>
        /// 让 Windows 控制台解释 ANSI 转义序列（TUI 的框和光标定位都依赖它）。
        /// 失败也不影响运行 —— 最坏情况是退化成「能玩但没框」的样子，
        /// 所以这里静默忽略异常，绝不因为开不了 VT 就崩掉游戏。
        /// </summary>
        private static void TryEnableAnsi()
        {
            try
            {
                IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return;

                if (!GetConsoleMode(handle, out uint mode)) return;
                if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0) return;   // 已经开了（如 Windows Terminal）

                SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
            }
            catch
            {
                // 非 Windows / 拿不到句柄 / 输出被重定向：忽略即可
            }
        }

        /**
         * 巨型 "LOSTCOMPOUND" 加粗斜体 banner。
         * 每个字母 6 格宽 x 5 行高，笔画加粗（横 == / 竖 ||），
         * 字母之间空 5 格，斜体用逐行缩进 4/3/2/1/0 实现。
         * 整幅约 131 列，终端建议 >=140 列。
         */
        private static readonly string[] BANNER = [
                "    ||         /====\\     /====\\     ======     /====\\     /====\\     ||  ||     |====\\     /====\\     ||  ||     ||  ||     |====\\",
                "   ||         ||  ||     ||           ||       ||          ||  ||     ||\\/||     ||  ||     ||  ||     ||  ||     ||\\ ||     ||  ||",
                "  ||         ||  ||     \\====|       ||       ||         ||  ||     ||  ||     |====/     ||  ||     ||  ||     || \\||     ||  ||",
                " ||         ||  ||          ||       ||       ||         ||  ||     ||  ||     ||         ||  ||     ||  ||     ||  ||     ||  ||",
                "||====     \\====/     \\====/       ||       \\====/     \\====/     ||  ||     ||         \\====/     \\====/     ||  ||     |====/"
        ];

        public static void Main(string[] args)
        {
            // 必须在任何输出之前开启：banner 之后马上就会用到 ANSI 定位
            TryEnableAnsi();

            PrintBanner();
            GameLobby lobby = new GameLobby();
            lobby.Run();
            Console.WriteLine("游戏已退出。");
        }

        private static void PrintBanner()
        {
            Console.WriteLine();
            foreach (string line in BANNER)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();
        }
    }
}
