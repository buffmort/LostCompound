using LostCompound.MapEnvironment;
using LostCompound.Maps;
using LostCompound.Rooms;

using static LostCompound.MapEnvironment.EnvType;

namespace LostCompound.Tools
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public class GameLobby
    {

        private bool running = true;

        public GameLobby()
        {
        }

        public void start()
        {
            while (running)
            {
                printMenu();
                int choice = readChoice();
                handleChoice(choice);
            }
        }

        private void printMenu()
        {
            Console.WriteLine("------------ 主菜单 ------------");
            Console.WriteLine("  1. 开始游戏");
            Console.WriteLine("  2. 退出游戏");
            Console.WriteLine("--------------------------------");
        }

        private void handleChoice(int choice)
        {
            switch (choice)
            {
                case 1:
                    startRun();
                    break;
                case 2:
                    exit();
                    break;
                default:
                    Console.WriteLine("没有这个选项。\n");
                    break;
            }
        }

        private void startRun()
        {
            Console.WriteLine();
            Console.WriteLine(">>> 开始游戏");
            Console.WriteLine();
            Console.WriteLine();
            TreeMazeGenerator tmg = new TreeMazeGenerator(10, 10, LAVA_WATERFALL);
            tmg.generate();
            Room[][] map = tmg.getGrid();
            Player p = new Player();
            p.startGame(map, tmg);
        }

        private void exit()
        {
            Console.Write("确定退出？(y/n): ");
            string line = Console.ReadLine().Trim();
            if (string.Equals(line, "y", StringComparison.OrdinalIgnoreCase))
            {
                running = false;
            }
            else
            {
                Console.WriteLine();
            }
        }

        /** 读一个菜单选项，输入非数字不会崩，会一直重问 */
        private int readChoice()
        {
            while (true)
            {
                Console.Write("请选择: ");
                string line = Console.ReadLine().Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                try
                {
                    return int.Parse(line);
                }
                catch (FormatException)
                {
                    Console.WriteLine("请输入数字。");
                }
            }
        }
    }
}
