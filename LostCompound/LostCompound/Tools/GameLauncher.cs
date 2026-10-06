namespace LostCompound.Tools
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public static class GameLauncher
    {

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
