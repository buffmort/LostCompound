using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Maps;
using LostCompound.UI;
using LostCompound.Buffs;
using LostCompound.Items.Capabilities;
using LostCompound.Items.Materials;
using LostCompound.Items.Consumables;
using LostCompound.Items.Containers;
using LostCompound.Items.Equipment;
using LostCompound.Rooms.Types;
using LostCompound.Tools.Debug;
using LostCompound.Buffs.Types;

namespace LostCompound.Tools
{
    public partial class Player
    {

        // 这里原先有一个 private bool _debug —— 它只在 255 里被写、只有 UnlockAllRecipes 读，
        // 与主菜单（GameLobby）里那个同名字段是两份互不相通的状态。
        // 现在调试开关统一为 DebugState.Enabled（见 DebugState 的说明）。

        /// <summary>
        /// 输入流是否已经结束（EOF）。
        ///
        /// 为什么需要它：<c>Console.ReadLine()</c> 在管道关闭 / 重定向读完 / Ctrl+Z 之后
        /// 会一直返回 null。这时任何「重问直到输入合法」的循环都不再读得到输入，
        /// 只会空转刷屏。ReadInt 读到 null 就置位本标志，让上层（战斗循环、开局方向循环）
        /// 能分辨「玩家输错了」（继续重问）和「输入已经没了」（必须收场）。
        /// </summary>
        private bool _inputClosed;

        /// <summary>
        /// 输入流是否已结束（EOF）。见 <see cref="_inputClosed"/>。
        /// 战斗循环用它决定「还要不要继续问玩家要操作」。
        /// </summary>
        public bool InputClosed => _inputClosed;

        // ---- TUI 状态栏联动 ----
        private ConsoleScreen _screen;          // 当前接管输出的分栏界面（不在游戏中时为 null）
        private string _statusHeader = "";      // 状态栏固定行：当前房间
        private bool _inBattle;                 // 是否处于战斗中（决定状态栏是否追加血量等）

        /// <summary>
        /// 玩家的「行动」部分：展示战场与可选项、读取输入并执行。
        /// 回合开始的簿记放在 OnTurnStart，跳过检查由战斗循环统一做 ——
        /// 所以这里只管"没被跳过时该干什么"。
        /// </summary>
        public void OnPlayerTurn(List<Monster> monsters, EnvType envType)
        {
            if (!IsAlive)
            {
                Console.WriteLine("你死了！");
                IsDead = true;
                return;
            }
            // 注意：流血不在这里结算。大回合开场的 Tools.ApplyBuffs 已经统一
            // 结算过一次，原先在此再扣一次会让玩家每回合吃两次持续伤害、时长掉两倍快。
            // （DOT 机制删除前同样的坑就在这里，口径没变。）
            // 背包中的「可转变道具」（白磷 / 热硫 / 木头等）与耐久结算已经在
            // OnTurnStart → HandleChangeableItems 里处理完了；这里只做回合内的展示。

            // 注意：buff 的状态快照（每回合伤害 / 剩余回合）原先在这里往日志打两行，
            // 现已整段删除 —— 这些信息改由右列状态框下方的「Buff 框」常驻显示（见 RefreshBuffBox），
            // 日志省下这些空间给真正的事件流（谁打了谁、掉了多少血）。
            // 敌人列表不再写进日志区，改由左列顶部的「敌人框」常驻显示（见 ConsoleScreen.DrawEnemies）。
            // 为什么搬走：Attack / SelectTarget / OpenItems 的提示都要求玩家按编号选目标，
            // 编号必须一直挂在眼前；写进滚动日志的话，几行之后就被冲掉了。
            RefreshEnemyBox(monsters);
            Console.WriteLine();
            Console.WriteLine("请选择操作");
            Console.WriteLine("1.攻击  2.防御  3.使用背包道具  4.徒手合成  5.饰品栏");
            if (DebugState.Enabled)
            {
                Console.WriteLine("debug模式操作:");
                Console.WriteLine("127, 超雄模式");
                Console.WriteLine("128, 获得全部合成");
                // 129 是新增的：战斗中当场拿道具（主菜单的「4. 获取道具」发到常驻调试玩家身上，
                // 战斗中由这里拿到正在战斗的这个玩家身上）。编号避开 127/128/255。
                Console.WriteLine("129, 获取道具");
                // 130 同理：战斗中当场挂 buff（ID → 效果量 → 剩余回合），逻辑见 DebugMenu.AddBuffTo。
                Console.WriteLine("130, 添加buff");
            }
            int option = ReadInt("请选择操作");
            if (option == -1 && _inputClosed)
            {
                // 输入流已结束：本回合没法行动，直接放弃这次行动返回。
                // 不落到下面的 default —— 那句「你输入的类型不正确！」是给手输错的玩家看的，
                // EOF 不是玩家输错。战斗循环会在这一回合之后看到 InputClosed 并收场（见 Tools.RunBattle）。
                return;
            }
            switch (option)
            {
                case 1:
                    Attack(monsters);
                    break;
                case 2:
                    // 原为 Block += 1（次数制，一次全免）；改为加 5 点护盾值。
                    // 数值理由：这是玩家唯一稳定可控的「攒盾」手段，而玩家面对的单次
                    // 最高伤害是龙的 10 点、双子技能的 5 点 —— 5 点盾大致等于「挡掉半刀」，
                    // 一回合换半刀比旧机制「一回合换一整刀」弱得多，符合「旧机制太超模」的裁定。
                    AddShield(5);
                    Console.WriteLine("你进行了防御!");
                    break;
                case 3:
                    OpenItems(monsters);
                    break;
                case 4:
                    HandCraft();
                    break;
                case 5:
                    OpenAccessoryMenu();
                    break;
                // 127 / 128 / 129 / 130 是调试指令，必须**先开开关**（255）才可用。
                //
                // 为什么写成 case ... when 而不是在 case 里 if：
                //   守卫不成立时这条 case 标签就不匹配，switch 会继续往下找，
                //   最后落到 default —— 也就是和「乱输一个数字」走**完全相同**的分支
                //   （同一句「你输入的类型不正确！ 已自动跳过回合」，同样不消耗额外的东西）。
                //   刻意不为这种情况新增任何提示：否则玩家一乱按就能发现「这里藏着东西」，
                //   而改造前它们是无条件生效的（127/128/129 一个守卫生效都没有）。
                case 127 when DebugState.Enabled:
                    AddBuff(new SuperMaleBuff(1,int.MaxValue));
                    Console.WriteLine($"超雄模式：基础 STR 已设为 {BaseStr}（当前 STR {Str}）");
                    break;
                case 128 when DebugState.Enabled:
                    UnlockAllRecipes();
                    break;
                case 129 when DebugState.Enabled:
                    // 定量发放任意道具（ID → 名称 → 数量）。
                    // 复用 DebugMenu 的那张 ItemFactory 表，不在这里再抄一份 ID 列表。
                    DebugMenu.GiveItems(this);
                    break;
                case 130 when DebugState.Enabled:
                    // 战斗内挂任意 buff（ID → 效果量 → 剩余回合）。
                    // 与 129 同一结构：清单、选择、容错全在 DebugMenu 里，这里只转发一行，
                    // 免得「主菜单能挂、战斗中不能挂」两套逻辑各写一遍（CONVENTIONS 2.5）。
                    DebugMenu.AddBuffTo(this);
                    break;
                case 255:
                    // 255 只负责开开关（并打印提示）—— 现在它真的有意义了：
                    // 127/128/129/130 与主菜单的调试项都读同一个 DebugState。
                    DebugState.Enable();
                    Console.WriteLine("你已进入debug模式!");
                    break;
                default:
                    Console.WriteLine("你输入的类型不正确！ 已自动跳过回合");
                    break;
            }
        }

        /// <summary>
        /// 读一个整数。输入非法时提示并重新读取，绝不抛异常。
        /// 替代原先裸用的 int.Parse(Console.ReadLine()) —— 那样在菜单里输入一个字母就会让整个游戏崩溃。
        /// prompt 会同时显示在右侧日志与输入框里（输入框是 TUI 的光标所在处）。
        ///
        /// 返回 <c>-1</c> 表示**输入流已结束（EOF）**，调用方按「取消」处理；
        /// 同时 <see cref="InputClosed"/> 会被置位，供上层（战斗循环 / 开局方向循环）收场。
        ///
        /// 为什么用 -1 当取消哨兵：各调用点的提示里本来就写着「-1取消」「-1即退出」
        /// （饰品栏、背包），-1 从来不是任何合法编号（编号一律从 0 起），所以不会与真实输入撞车，
        /// 也不需要给 5 个调用点各改一次签名。
        ///
        /// ⚠️ 空行（<c>""</c>）不是 EOF：空行按原来的「输入无效」重问 —— 那是玩家有意不选，
        /// 与「输入流没了」是两件事，不能混为一谈。
        /// </summary>
        private int ReadInt(string prompt)
        {
            while (true)
            {
                // 输入框里也显示一份提示，否则玩家只能盯着滚动日志看要输入什么
                _screen?.SetPrompt(prompt);
                string line = Console.ReadLine();
                if (line == null)
                {
                    // EOF：再循环一次 ReadLine 还是立刻返回 null，重问就是不读输入的 CPU 空转 + 刷屏。
                    // 所以这里不重问，直接把这当成「取消」交回调用方。
                    _inputClosed = true;
                    return -1;
                }
                if (int.TryParse(line, out int value))
                {
                    return value;
                }
                Console.WriteLine("输入无效，请输入数字。");
            }
        }

        /// <summary>刷新状态栏。战斗时会追加血量/防御/护盾/力量。</summary>
        private void RefreshStatus()
        {
            if (_screen == null) return;
            string s = _statusHeader;
            if (_inBattle)
            {
                // 护盾按 F1 保留一位小数：它是 double，且装备加成将来可能是小数（如盾 +5.0）
                s += "\n血量:" + Hp.ToString("F1") + "  防御:" + Def.ToString("F1") + "  护盾:" + Shield.ToString("F1");
                // 显示 STR（力量）而不是最终的攻击力：攻击力 = 2 + STR × √ATK 是派生值，
                // 而 STR 才是环境（如熔岩瀑布缺水）与装备真正在改的东西，
                // 看它才知道「自己是被削了还是被加强了」。
                s += "\n力量:" + Str.ToString("F1");
                // s += "\n攻击力:" + Atk.ToString("F1");
            }
            Environment env = CurrentEnvironment;
            if (env != null) s += "\n" + env.DescribeStatus();
            _screen.DrawStatus(s);
        }

        /// <summary>
        /// 把战场上的敌人列表喂给左列顶部的「敌人框」。
        ///
        /// 格式：每行 "[i] 僵尸(30.0)"，最多 <see cref="ConsoleScreen.MaxEnemyRows"/> 行；
        /// 不足的行由 DrawEnemies 自己补空。
        /// 先把实际怪物数交给 <see cref="ConsoleScreen.SetEnemyRows"/>：它会调整 EnemyRows
        /// 并重排左列（日志框 / 输入框都会跟着上下移动），然后再画内容 ——
        /// 顺序不能反，否则 SetEnemyRows 里的整屏重排会用「旧 EnemyRows」重画敌人框。
        /// 0 只怪时 SetEnemyRows 会夹到 1 行（空框仍是「这里没有敌人」的提示）。
        /// 行尾的空格要 TrimEnd：Monster.ToString() 结尾自带一个空格（那是给
        /// SelectTarget 的 "0. 僵尸(30.0)   1. ..." 横向排列用的），框内显示不需要它。
        /// </summary>
        private void RefreshEnemyBox(List<Monster> monsters)
        {
            if (_screen == null) return;

            _screen.SetEnemyRows(monsters.Count);

            List<string> lines = [];
            for (int i = 0; i < monsters.Count && i < ConsoleScreen.MaxEnemyRows; i++)
            {
                lines.Add("[" + i + "] " + monsters[i].ToString().TrimEnd());
            }
            _screen.DrawEnemies(string.Join("\n", lines));
        }

        /// <summary>
        /// 进入/离开战斗。战斗状态会影响状态栏内容；离开战斗时顺手清空敌人框。
        ///
        /// 「战斗结束清空敌人框」为什么放在这里 —— Tools.RunBattle 用 try/finally
        /// 保证无论怎么退出都会走 SetInBattle(false)，在 Player 这一侧清空能覆盖
        /// 全部战斗入口（FightRoom / BossRoom / DebugMenu），不必去动 Tools.cs。
        ///
        /// 不清的后果：最后一名敌人被打死那次，OnPlayerTurn 里的 RefreshEnemyBox
        /// 不会再被调用（怪死光后循环直接 break），框里会残留一个 0 血的敌人。
        /// </summary>
        public void SetInBattle(bool v)
        {
            _inBattle = v;
            if (v)
            {
                // 战斗计数：Tools.RunBattle 在开战时调用一次 SetInBattle(true)、
                // 在 finally 里调用一次 SetInBattle(false)，所以这里 +1 恰好是「每场战斗一次」。
                // 不要挪到 RunBattle 的循环里（那会按回合累加），也不要挪到 OnEnter
                // （调试战斗 / 将来的其它战斗入口会绕过房间）。
                AddFightCount();
                ApplyEquipmentPassives();
            }
            else
            {
                // 空战斗 = 0 只怪：敌人框缩回 1 行，把省下的纵向空间还给日志
                // （上限与夹取见 ConsoleScreen.SetEnemyRows）。
                _screen?.SetEnemyRows(0);
                _screen?.DrawEnemies("");         // 空串 = 敌人框只剩 1 行空白
                _screen?.DrawBuffs("");           // 同理清空 Buff 框（Tools.ClearBuffs 也会清掉 Buffs 列表）
            }
            // 进/出战斗都要重算派生值：水量的修正、buff 的修正都得反映进来
            RecomputeStats();
            RefreshStatus();
        }

        /// <summary>
        /// 用分区布局重画「地图」与「状态」两块固定区域。
        /// 注意这里不再清屏：地图固定在屏幕上方，日志在下方滚动，两者互不影响。
        /// </summary>
        private void DrawScreen(ConsoleScreen screen, Room[][] map, TreeMazeGenerator tmg)
        {
            screen.DrawMap(tmg.ToDisplayString());

            // 位置/房间这两行是状态栏的固定部分，缓存起来供战斗中刷新时复用
            // 「当前位置」已按要求去掉：地图上玩家所在格子本来就有 * 标记，再报一遍坐标是冗余。
            _statusHeader = "当前房间:" + map[PlayerRow][PlayerCol].Type;

            RefreshStatus();
        }

        public void StartGame(Room[][] map, TreeMazeGenerator tmg)
        {
            JoinMap(map);

            // 地图文本的行数/列数决定地图区的大小（行数 = rows*2+1，列数算最长一行）
            string[] mapLines = tmg.ToDisplayString().Replace("\r", "").Split('\n');
            int mapColumns = mapLines.Max(line => line.Length);
            ConsoleScreen screen = new ConsoleScreen(mapLines.Length, mapColumns);
            screen.Install();
            _screen = screen;              // 供 RefreshStatus / ReadInt 使用
            Console.WriteLine("游戏开始！输入方向探索地图。");

            // 开局解锁「配方0」（内容见 AddDefaultRecipes；原先这里括注「热硫 + 玻璃罐」，
            // 早已漏掉水瓶，这次补全配方 0 后更不完整，故去掉括注、只留方法名这一处真相）。
            // 历史：这个方法原先只在一个已删除的调试入口里被调用，主游戏从来没解锁过任何配方，
            // 导致玩家选「4.徒手合成」永远只会看到「你没有任何道具合成!」。
            // 放在 Install 之后，提示才会进日志区（放前面会被 Install 的清屏抹掉）。
            AddDefaultRecipes();
            // 这样玩家先看到自己身处何地，再看到房间里发生了什么。
            bool pendingEnter = true;    // 起始房间的事件也要等第一张地图
            bool justMoved = false;

            try
            {
                while (IsAlive)
                {
                    // ---- 1. 地图 + 状态（分区绘制，不会影响日志区）----
                    DrawScreen(screen, map, tmg);
                    if (justMoved)
                    {
                        Console.WriteLine("你移动到了新房间。");
                        justMoved = false;
                    }

                    // ---- 2. 房间事件（宝箱、战斗等）----
                    if (pendingEnter)
                    {
                        pendingEnter = false;
                        map[PlayerRow][PlayerCol].OnEnter(this);
                    }

                    // ---- 3. 事件可能改变了状态（如水量），重画一次 ----
                    DrawScreen(screen, map, tmg);

                    ShowAvailableDirections();

                    // 输入框里也提示一次可走的方向，别让玩家只看到日志区在滚
                    _screen?.SetPrompt("选择方向（0上 1下 2左 3右，4 退出）");
                    string line = Console.ReadLine();
                    if (line == null)
                    {
                        // 输入流结束（EOF）：方向永远读不到了。必须退出游戏循环 ——
                        // 原先下面的 continue 会立刻再读到 null，主循环变成不读输入的 CPU 空转 + 刷屏。
                        // 退出效果与玩家输入 4 相同（同一句结束语）。
                        _inputClosed = true;
                        Console.WriteLine("游戏结束，感谢游玩！");
                        break;
                    }
                    if (!int.TryParse(line, out int direction))
                    {
                        Console.WriteLine("输入无效，请输入数字。");
                        continue;
                    }
                    if (direction == 4)
                    {
                        Console.WriteLine("游戏结束，感谢游玩！");
                        break;
                    }

                    if (Move((Direction)direction))
                    {
                        pendingEnter = true;
                        justMoved = true;
                    }
                }
            }
            finally
            {
                // 离开地图时把 Console.Out 还给真实输出，否则主菜单的输出会跑进日志区
                screen.Uninstall();
                _screen = null;
            }
        }
    }
}
