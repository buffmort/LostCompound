namespace LostCompound.Tools
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public static class GameLauncher
    {
        // ⚠️ 关于「裸跑 exe 时满屏 [41;1H」这件事（曾经有个 TryEnableAnsi 解决它，已按用户要求删除）：
        //   Windows 的传统控制台（conhost）默认**不解释** ANSI 转义序列，而本项目的 TUI
        //   全靠 \u001b[row;colH 定位。所以直接双击 exe 时会把转义码当普通文字打出来。
        //   Rider 的 Run 窗口、Windows Terminal 都自己解析 ANSI，所以在 IDE 里看不出问题。
        //
        //   删掉那个方法之后，直接双击 exe 会显示错乱。要正常游玩请用二选一：
        //     ① 用 Play.ps1 启动（它借 PowerShell 宿主，行为已由该脚本处理）；
        //     ② 在 Windows Terminal 里跑（它原生支持 ANSI，无需任何设置）。
        //
        //   如果将来又想支持「双击 exe 直接玩」，把 SetConsoleMode 开
        //   ENABLE_VIRTUAL_TERMINAL_PROCESSING (0x0004) 那段加回来、并在 Main 最开始调用即可。

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
