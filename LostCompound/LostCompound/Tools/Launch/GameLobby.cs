using LostCompound.MapEnvironment;
using LostCompound.Maps;
using LostCompound.Rooms;

using static LostCompound.MapEnvironment.EnvType;
using LostCompound.Tools;
using LostCompound.Tools.Debug;

using LostCompound.Tools.Players;
using LostCompound.Save;

namespace LostCompound.Tools.Launch
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public class GameLobby
    {

        private bool _running = true;

        // 这里原先有一个 private bool _debug —— 与 Player 里那个同名字段是**两份状态**，
        // 必然不一致（主菜单开了调试、战斗里读不到，反之亦然）。现已整体换成
        // 全局的 DebugState.Enabled（见 DebugState 的说明），两边读的是同一个开关。

        public void Run()
        {
            AskNameIfNeeded();

            while (_running)
            {
                PrintMenu();
                int? choice = ReadChoice();
                if (choice == null)
                {
                    // 输入流结束（EOF）：输入已经没有了，再刷一遍主菜单毫无意义 ——
                    // 直接结束主菜单循环，等价于玩家选了「2. 退出游戏」。
                    // 注意不能改成 continue：ReadChoice 会立刻再返回 null，主菜单变成不读输入的空转刷屏。
                    Console.WriteLine();
                    _running = false;
                    break;
                }
                HandleChoice(choice.Value);
            }
        }

        /// <summary>
        /// 第一次运行时问一次名字，存进 profile（`%APPDATA%\LostCompound\profile.json`）。
        ///
        /// 为什么在**主菜单之前**问：镜子(45) 与电子云的提示语里要显示名字，
        /// 而那些道具随时能用，与「开新局 / 继续游戏」无关；先问一次，后面两处都能直接用。
        /// 已经起过名就不再问（改了想改名，删掉 profile.json 即可 —— 不值得为它加一个菜单项）。
        ///
        /// 回车 / EOF 都走默认名「玩家」：不能因为玩家没输入名字就把开局拦下来。
        /// </summary>
        private static void AskNameIfNeeded()
        {
            if (!Profile.HasNoName)
            {
                return;
            }

            Console.WriteLine("请输入你的名字（直接回车 = 用「" + Profile.DefaultName + "」）：");
            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                input = Profile.DefaultName;
            }

            Profile.Rename(input);
            Console.WriteLine("你好，" + Profile.Name + "！\n");
        }

        private void PrintMenu()
        {
            Console.WriteLine("------------ 主菜单 ------------");

            // 有存档时多出「继续游戏」，所以 1 是继续、2 是新游戏、3 是退出。
            // 没有存档时保持原来的 1 开始 / 2 退出 —— 玩家看不出区别，也不会误点到一个空选项。
            bool hasSave = SaveFile.Exists;
            if (hasSave)
            {
                Console.WriteLine("  1. 继续游戏");
                Console.WriteLine("  2. 开始新游戏");
                Console.WriteLine("  3. 退出游戏");
            }
            else
            {
                Console.WriteLine("  1. 开始游戏");
                Console.WriteLine("  2. 退出游戏");
            }

            // debug 项永远排在最后两个：编号按「前面有几项」算，避免手抄两套编号。
            if (DebugState.Enabled)
            {
                Console.WriteLine("  " + DebugBattleChoice + ". 调试战斗");
                Console.WriteLine("  " + GiveItemsChoice + ". 获取道具");
            }
            Console.WriteLine("--------------------------------");
        }

        // 菜单编号集中在这里算，PrintMenu 与 HandleChoice 共用同一份 ——
        // 各写一遍的话，「有存档时 3 是调试战斗」这种偏移早晚会对不上。
        private static bool HasSave => SaveFile.Exists;
        private static int ContinueChoice => 1;
        private static int NewGameChoice => HasSave ? 2 : 1;
        private static int ExitChoice => HasSave ? 3 : 2;
        private static int DebugBattleChoice => HasSave ? 4 : 3;
        private static int GiveItemsChoice => HasSave ? 5 : 4;

        private void HandleChoice(int choice)
        {
            // 255 是「开启 debug 模式」而不是菜单项，所以它不出现在菜单里，先单独处理。
            if (choice == 255)
            {
                // 这里仅开开关，不做别的：菜单项的行为完全不变。
                // 开的是全局开关（DebugState），所以战斗中输 255 也开的是同一个，
                // 两处不会再各开一份、各看一份。
                DebugState.Enable();
                Console.WriteLine("已进入 debug 模式！\n");
                return;
            }

            if (HasSave && choice == ContinueChoice)
            {
                ContinueRun();
                return;
            }
            if (choice == NewGameChoice)
            {
                StartNewRun();
                return;
            }
            if (choice == ExitChoice)
            {
                ConfirmExit();
                return;
            }
            if (choice == DebugBattleChoice)
            {
                if (!DebugState.Enabled)
                {
                    Console.WriteLine("没有这个选项。\n");
                    return;
                }
                DebugMenu.RunDebugBattle();
                return;
            }
            if (choice == GiveItemsChoice)
            {
                if (!DebugState.Enabled)
                {
                    Console.WriteLine("没有这个选项。\n");
                    return;
                }
                DebugMenu.GiveItems();
                return;
            }

            Console.WriteLine("没有这个选项。\n");
        }

        /// <summary>
        /// 从存档开局。
        ///
        /// 分工：<see cref="SaveFile"/> 管文件，<see cref="SaveRestorer"/> 管「数据 → 对象」，
        /// 这里只做「读失败就说人话、读成功就进场」。
        /// 地图不重新生成 —— 存档里带着地形，重生成等于把玩家走过的路抹掉。
        /// </summary>
        private void ContinueRun()
        {
            Console.WriteLine();
            Console.WriteLine(">>> 继续游戏");

            SaveData data = SaveFile.Load();
            if (data == null)
            {
                // Load 内部已经打印过具体原因（无档/损坏/版本不符）
                Console.WriteLine("读取存档失败，已返回主菜单。\n");
                return;
            }

            Player p = new Player();
            // 地图在 Restorer 里用**种子重新生成**，所以它会连生成器一起交回来
            if (!SaveRestorer.TryRestore(data, p, out Room[][] map, out TreeMazeGenerator tmg))
            {
                Console.WriteLine("存档内容无法还原，已返回主菜单。\n");
                return;
            }

            int row = Math.Clamp(data.Player.Row, 0, map.Length - 1);
            int col = Math.Clamp(data.Player.Col, 0, map[0].Length - 1);

            Console.WriteLine($"已读取存档（{data.SavedAt}），种子 {data.Seed}。");
            p.ContinueGame(map, tmg, row, col);
        }

        private void StartNewRun()
        {
            Console.WriteLine();
            Console.WriteLine(">>> 开始游戏");
            Console.WriteLine();
            Console.WriteLine();
            // 先让玩家挑本层环境，再用挑到的环境建图。
            // 注意：类型名必须写全 EnvType —— 环境类 LostCompound.MapEnvironment.LavaWaterfall
            // 与枚举成员同名，用简单名会产生二义性（CS0229）。
            // SelectEnvironment 返回 null = 输入流结束、没得选（见下面的处理）。
            EnvType? envType = SelectEnvironment();
            if (envType == null)
            {
                // 输入流结束：环境没得选，这一层就不建了（主菜单随后会自己退出）。
                // 这样 EOF 下不会先花时间建图、再进一局注定立刻退出的游戏。
                Console.WriteLine("已取消开局。\n");
                return;
            }
            // 摇一个本局**总种子**。整局的地图都由它派生：
            //   第 N 层的种子 = GameProgress.SeedForLayer(总种子, N)
            // 存档里存总种子 + 层数 + 当前层种子，读档时能重算出同一张图，
            // 而且继续往下打时第 N+1 层的种子也能算出来（不用把 100 格地形写进文件）。
            long seed = Random.Shared.NextInt64();
            Console.WriteLine("本局种子：" + seed);

            // 开新局 = 从第一层开始。上一局打到第几层都不该带进来
            //（读档路径不走这里，它用存档里记的层数，见 SaveRestorer）。
            GameProgress.StartRun(seed);

            TreeMazeGenerator tmg = new TreeMazeGenerator(
                10, 10, envType.Value, GameProgress.LayerSeed, GameProgress.Layer);
            tmg.Generate();
            Room[][] map = tmg.Grid;
            Player p = new Player();
            p.StartGame(map, tmg);
        }

        /// <summary>
        /// 让玩家选择本层环境，返回对应的 <see cref="EnvType"/>。
        /// 返回 <c>null</c> = 输入流已结束（EOF），没能选出环境，由调用方取消这次开局。
        ///
        /// 清单来自 <see cref="EnvironmentCatalog"/>（只列**已实现**的环境：
        /// 未实现的环境会让 EnvironmentFactory.Create 返回 null，整层的环境机制
        /// （水量、水位等）就静默失效，玩家看到的是一张「没有环境」的地图。
        /// 所以每实现一个环境就往那个清单里加一行，而不是直接遍历 EnvType 全枚举）。
        /// 原先这份列表与 DebugMenu.SelectEnvironment 各抄一份，现在共用数据源。
        /// 文案与交互完全不变：编号从 1 开始、越界重问。
        ///
        /// 输入沿用 <see cref="ReadChoice"/> 的容错写法（TryParse），不引入 int.Parse。
        /// </summary>
        private EnvType? SelectEnvironment()
        {
            while (true)
            {
                Console.WriteLine("------------ 选择本层环境 ------------");
                for (int i = 0; i < EnvironmentCatalog.All.Count; i++)
                {
                    Console.WriteLine("  " + (i + 1) + ". " + EnvironmentCatalog.All[i].Name);
                }
                Console.WriteLine("--------------------------------------");

                int? choice = ReadChoice();
                if (choice == null)
                {
                    // 输入流结束：这里永远选不出环境了，重问只会空转。交回 null 让调用方取消开局。
                    Console.WriteLine();
                    return null;
                }
                if (choice.Value < 1 || choice.Value > EnvironmentCatalog.All.Count)
                {
                    // 越界就重问，和主菜单「没有这个选项。」保持同一套容错风格
                    Console.WriteLine("没有这个选项。\n");
                    continue;
                }

                (string Name, EnvType Type) pick = EnvironmentCatalog.All[choice.Value - 1];
                Console.WriteLine("你选择了：" + pick.Name);
                return pick.Type;
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
        /// 返回 <c>null</c> 表示**输入流已结束（EOF）**，调用方必须据此退出，不能再重问
        /// （ReadLine 之后每次都返回 null，重问就是空转）。
        ///
        /// ⚠️ 空行（<c>""</c>）不是 EOF：空行是玩家有意「重问」的语义，仍然 continue。
        /// 两者绝不能混为一谈 —— 见 <see cref="DebugMenu.ReadIntOrCancel"/>（那份把两者都当取消，
        /// 是因为它所在菜单的约定不同；这里按主菜单原有语义，只把 EOF 当退出理由）。
        /// </summary>
        private int? ReadChoice()
        {
            while (true)
            {
                Console.Write("请选择: ");
                // 同 ConfirmExit：ReadLine 在输入流结束时会返回 null，不能直接 .Trim()
                string line = Console.ReadLine();
                if (line == null)
                {
                    // EOF：把 null 交回调用方（Run / SelectEnvironment），由它决定怎么收场 ——
                    // 在这里自设 _running = false 会让「选择本层环境」这条路径也悄悄退出主菜单，
                    // 语义上说不通（那时玩家还没进游戏，取消的应该是开局）。
                    return null;
                }
                line = line.Trim();
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
