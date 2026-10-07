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
            // 先让玩家挑本层环境，再用挑到的环境建图。
            // 注意：必须写 EnvType.LavaWaterfall。简单名 LavaWaterfall 现在与环境类
            // LostCompound.MapEnvironment.LavaWaterfall 同名，会产生二义性（CS0229）。
            EnvType envType = SelectEnvironment();
            TreeMazeGenerator tmg = new TreeMazeGenerator(10, 10, envType);
            tmg.Generate();
            Room[][] map = tmg.Grid;
            Player p = new Player();
            p.StartGame(map, tmg);
        }

        /// <summary>
        /// 让玩家选择本层环境，返回对应的 <see cref="EnvType"/>。
        ///
        /// 这里只列**已实现**的环境：未实现的环境会让 EnvironmentFactory.Create 返回 null，
        /// 整层的环境机制（水量、水位等）就静默失效，玩家看到的是一张「没有环境」的地图。
        /// 所以每实现一个环境就往这张表里加一行，而不是直接遍历 EnvType 全枚举。
        ///
        /// 输入沿用 <see cref="ReadChoice"/> 的容错写法（TryParse），不引入 int.Parse。
        /// </summary>
        private EnvType SelectEnvironment()
        {
            while (true)
            {
                Console.WriteLine("------------ 选择本层环境 ------------");
                Console.WriteLine("  1. 熔岩瀑布");
                Console.WriteLine("  2. 水淹洞穴");
                Console.WriteLine("--------------------------------------");

                int choice = ReadChoice();
                switch (choice)
                {
                    case 1:
                        Console.WriteLine("你选择了：熔岩瀑布");
                        return EnvType.LavaWaterfall;
                    case 2:
                        Console.WriteLine("你选择了：水淹洞穴");
                        return EnvType.FloodedCavern;
                    default:
                        // 越界就重问，和主菜单「没有这个选项。」保持同一套容错风格
                        Console.WriteLine("没有这个选项。\n");
                        break;
                }
            }
        }

        private void ConfirmExit()
        {
            Console.Write("确定退出？(y/n): ");
            // Console.ReadLine() 在输入流结束（管道关闭 / Ctrl+Z / 重定向）时返回 null，
            // 直接 .Trim() 会 NullReferenceException 崩掉整个程序。
            string line = (Console.ReadLine() ?? "").Trim();
            if (string.Equals(line, "y", StringComparison.OrdinalIgnoreCase))
            {
                _running = false;
            }
            else
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 读一个菜单选项：非数字与超出 int 范围的输入都不会崩，会一直重问。
        /// </summary>
        private int ReadChoice()
        {
            while (true)
            {
                Console.Write("请选择: ");
                // 同 ConfirmExit：ReadLine 在输入流结束时会返回 null，要先兜住
                string line = (Console.ReadLine() ?? "").Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                // 用 TryParse 而不是 int.Parse：int.Parse 遇到超长数字会抛 OverflowException，
                // 和 Player.ReadInt 保持同一种写法。
                if (int.TryParse(line, out int choice))
                {
                    return choice;
                }
                Console.WriteLine("请输入数字。");
            }
        }
    }
}
