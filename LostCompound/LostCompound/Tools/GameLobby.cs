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

        private bool _running = true;

        public GameLobby()
        {
        }

        public void Run()
        {
            while (_running)
            {
                PrintMenu();
                int choice = ReadChoice();
                HandleChoice(choice);
            }
        }

        private void PrintMenu()
        {
            Console.WriteLine("------------ 主菜单 ------------");
            Console.WriteLine("  1. 开始游戏");
            Console.WriteLine("  2. 退出游戏");
            Console.WriteLine("--------------------------------");
        }

        private void HandleChoice(int choice)
        {
            switch (choice)
            {
                case 1:
                    StartNewRun();
                    break;
                case 2:
                    ConfirmExit();
                    break;
                default:
                    Console.WriteLine("没有这个选项。\n");
                    break;
            }
        }

        private void StartNewRun()
        {
            Console.WriteLine();
            Console.WriteLine(">>> 开始游戏");
            Console.WriteLine();
            Console.WriteLine();
            // 注意：必须写 EnvType.LavaWaterfall。简单名 LavaWaterfall 现在与环境类
            // LostCompound.MapEnvironment.LavaWaterfall 同名，会产生二义性（CS0229）。
            TreeMazeGenerator tmg = new TreeMazeGenerator(10, 10, EnvType.LavaWaterfall);
            tmg.Generate();
            Room[][] map = tmg.Grid;
            Player p = new Player();
            p.StartGame(map, tmg);
        }

        private void ConfirmExit()
        {
            Console.Write("确定退出？(y/n): ");
            string line = Console.ReadLine().Trim();
            if (string.Equals(line, "y", StringComparison.OrdinalIgnoreCase))
            {
                _running = false;
            }
            else
            {
                Console.WriteLine();
            }
        }

        /** 读一个菜单选项，输入非数字不会崩，会一直重问 */
        private int ReadChoice()
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
