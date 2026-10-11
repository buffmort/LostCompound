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
        //
        // 背景：Windows 的传统控制台（conhost）默认**不解释** ANSI 转义序列，
        // 而本项目的 TUI 全靠 \u001b[row;colH 定位。所以裸跑 exe 时会把转义码
        // 当普通文字打出来，满屏 "[41;1H│"。
        //
        // 为什么必须由程序自己开、不能指望环境：
        //   · Rider 的 Run 窗口、Windows Terminal 都自己解析 ANSI，所以在 IDE 里看不出问题；
        //   · 但「双击 exe」走哪个宿主取决于**别人机器上的设置**
        //     （HKCU\Console\%%Startup 的默认终端、以及 HKCU\Console\VirtualTerminalLevel）。
        //     实测过：装了 Windows Terminal 但如果没被设为默认终端，双击照样是 conhost、照样花屏。
        //   · 这是要发给别人的 exe，不能要求对方去改系统设置 —— 只能自己开。
        //
        // 历史教训：这个方法一度被当作「零调用死代码」删掉，结果换个文件夹运行就花屏
        //   （新路径没有历史控制台设置，注册表里那一条不存在）。别再删了。
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

        /// <summary>
        /// 正式入口。
        /// 想单独跑别的东西（如调试菜单里的战斗）请用 Rider 的运行配置指定启动对象，
        /// 不要动这里的名字 —— 曾经把它临时改名、导致发布的 exe 直接进了战斗，没有主菜单。
        /// </summary>
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
