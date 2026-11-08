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
using LostCompound.Save;

namespace LostCompound.Tools.Players
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

        /// <summary>
        /// 状态栏第一行「当前房间:xxx」用的房间类型。
        ///
        /// 为什么存**类型**而不是像以前那样存拼好的整行字符串（原 `_statusHeader`）：
        /// 那行里还带着玩家名（镜子会把名字改成「左旋-张三」），拼好缓存起来之后，
        /// 名字一变、这行就永远是旧的 —— 战斗中整场显示旧名字，因为战斗中不会再调 DrawScreen。
        /// 现在改成每次 RefreshStatus 现拼，缓存只留房间类型（它不会在同一步里变）。
        /// </summary>
        private RoomType _lastRoomType = RoomType.Start;

        private bool _inBattle;                 // 是否处于战斗中（决定状态栏是否追加血量等）

        /// <summary>
        /// 「换层之后，等起点房的 OnEnter 走完再存一次档」的牌子。
        ///
        /// 为什么不在 <see cref="AdvanceLayer"/> 里直接存：那一刻新层的起点房还没进，
        /// `StartRoom.StartItemsGiven` 仍是 false。若此时存档、进程随即被杀，
        /// 读档会得到一个「开局三选一还没发」的状态 —— 而读档路径 pendingEnter=false，
        /// 不会补发，玩家就白白少了一轮开局道具（要离开再回起始房才会补）。
        /// </summary>
        private bool _saveAfterEnter;

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
            Console.WriteLine("1.攻击  2.防御  3.打开背包  4.合成  5.饰品栏");

            // 第 6 项只在「这里能采东西」时出现（目前只有沙漠，DESIGN §5.3 采沙）。
            // 问的是环境能力位，不是 `env is Desert` —— 将来再有能采的环境不用改这里。
            bool canDigSand = CurrentEnvironment?.CanDigSand == true;
            if (canDigSand)
            {
                Console.WriteLine("6.采沙");
            }
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
                    CraftMenu();
                    break;
                case 5:
                    OpenAccessoryMenu();
                    break;
                case 6 when CurrentEnvironment?.CanDigSand == true:
                    // 采沙：一次行动换一堆沙子（DESIGN §5.3「花费 1 回合获得 5 沙子」）。
                    // 用 when 守卫而不是在 case 里 if：不满足时这条 case 不匹配，
                    // 与「乱输一个数字」走完全相同的 default 分支（不新增任何提示）。
                    foreach (Item dug in CurrentEnvironment.DigSand())
                    {
                        AddItem(dug);
                    }
                    Console.WriteLine("你花了些力气，从沙丘里筛出一堆沙子。");
                    // 采沙是「行动」不是「攻击」：它对怪物本身不造成伤害，
                    // 但它会把遁在沙里的怪（沙虫）翻出来 —— 通用循环只喊一声，
                    // 具体反应由怪物自己决定（见 Monster.OnSandDug）。
                    foreach (Monster monster in monsters)
                    {
                        monster.OnSandDug();
                    }
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
                    // ⚠️ 时长必须传 Buff.Permanent（-1 这个哨兵），**不能传负数回合**：
                    //   Buff 构造函数里是 `turns == Permanent ? Permanent : Math.Max(turns, 0)`，
                    //   所以 -12 会被夹成 0 → IsExpired 立刻为真 → buff 挂上就被回合管线清掉。
                    //   原先这里写的是 -12，超雄模式其实一直是「挂上即消失」（Hyberty反馈「buff 没了」）。
                    //   SurvivesBattleEnd 仍然为 true：这个调试 buff 本来就是常驻的。
                    AddBuff(new SuperMaleBuff(1, Buff.Permanent, true));
                    // 报「当前 STR」而不是 BaseStr：这个 buff 走的是修正量通道（StrBonus +1000），
                    // 并不改 BaseStr —— 原先那句「基础 STR 已设为 X」与事实不符。
                    Console.WriteLine($"超雄模式：STR +1000（永久，跨战斗保留）。当前 STR {Str}");
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
                // 提示写进日志区，而不是输入框。
                // 输入框现在只画 ">"：提示文字 + 终端回显叠在同一个框里，输入次数一多就会错位
                // （提示与词条重叠 / 上一句的残影），详见 ConsoleScreen.RedrawInputBox。
                Console.WriteLine(prompt);
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

        /// <summary>
        /// 道具用完之后刷新右侧两个框（状态框 + Buff 框）。
        ///
        /// 为什么需要它：道具的 <c>Use</c> 可以直接改玩家状态 —— 镜子(45) 就会对换
        /// Base 属性并夹低 Hp，而 <see cref="Player.OpenItems"/> 原先只做收尸（RemoveConsumedItems），
        /// 没有刷新界面。于是在战斗中用镜子，状态框里还是换之前的血量/防御/力量，
        /// 一直要等到怪物打中（TakeDamage 里会刷新）或下一回合才更新 —— 玩家会以为道具没生效。
        ///
        /// 为什么用 internal 而不是 public：这是**界面**的事，只有同程序集的道具使用路径
        /// 该调它；公开出去等于允许游戏逻辑在任何地方刷界面（CONVENTIONS 1.2 的可见性口径）。
        /// </summary>
        internal void RefreshAfterItemUse()
        {
            RefreshStatus();
            RefreshBuffBox();
        }

        /// <summary>刷新状态栏。战斗时会追加血量/防御/护盾/力量。</summary>
        private void RefreshStatus()
        {
            if (_screen == null) return;

            // 第一行现拼：房间类型 + 玩家名（名字会被镜子改成「左旋-张三」，不能缓存整行）
            string s = "当前房间:" + _lastRoomType + "  " + RunDisplayName;
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
        private void DrawScreen(ConsoleScreen screen, Room[][] map, IsaacMazeGenerator tmg)
        {
            screen.DrawMap(tmg.ToDisplayString());

            // 只记「在哪个房间」；状态栏首行由 RefreshStatus 现拼（房间 + 玩家名）。
            // 「当前位置」已按要求去掉：地图上玩家所在格子本来就有 * 标记，再报坐标是冗余。
            _lastRoomType = map[PlayerRow][PlayerCol].Type;

            RefreshStatus();
        }

        /// <summary>
        /// 按地图文本算出一个尺寸合适的分栏界面（**只算尺寸，不装**）。
        ///
        /// 抽出来的原因：正局（<see cref="RunMainLoop"/>）与调试战斗
        /// （<see cref="RunDebugBattleOnScreen"/>）都需要同一套尺寸规则 ——
        /// 各写一遍的话，哪天地图渲染改了列宽，总有一边会错位。
        /// </summary>
        private static ConsoleScreen CreateScreenFor(IsaacMazeGenerator tmg)
        {
            string[] mapLines = tmg.ToDisplayString().Replace("\r", "").Split('\n');
            int mapColumns = mapLines.Max(line => line.Length);
            return new ConsoleScreen(mapLines.Length, mapColumns);
        }

        /// <summary>
        /// **调试战斗专用**：装上分栏界面打一场，结束后自动还原输出。
        ///
        /// 为什么调试战斗也要有框：以前它直接往真实控制台打，敌人框 / 状态框 / Buff 框全都没有，
        /// 于是「多怪时敌人框自适应」「Buff 框排版」这些**只能在框里看出来的问题**，
        /// 调试战斗根本验不了 —— 只能开正局碰运气。
        ///
        /// 与正局的区别（都是有意为之）：
        ///   · 不调 SaveSession.Begin —— 调试战斗不是一局，不该被"关窗口自动存档"写进档案；
        ///   · 不解锁配方、不触发房间事件 —— 这里只打一场战斗，不进入地图循环；
        ///   · `pendingEnter` 之类的地图循环逻辑一概不参与，地图只用来撑版面 + 提供环境。
        /// </summary>
        /// <param name="map">调试用的地图（目前是 10×10 生成图，只为版面与环境）</param>
        /// <param name="monsters">这一场的敌人</param>
        /// <param name="env">环境对象（与 <c>map</c> 里房间用的是同一个实例）</param>
        public void RunDebugBattleOnScreen(Room[][] map, IsaacMazeGenerator tmg, List<Monster> monsters, Environment env)
        {
            ConsoleScreen screen = CreateScreenFor(tmg);
            screen.Install();
            _screen = screen;

            try
            {
                // 先把地图与状态画出来，玩家一进场就能看到框（而不是先看到一片空白）
                DrawScreen(screen, map, tmg);

                // 战斗本体：与正局完全同一条通路（Tools.RunBattle），
                // 所以敌人框 / 状态栏 / Buff 框 / 日志区在这里的行为与正局一致。
                Tools.RunBattle(this, monsters, env);
            }
            finally
            {
                // 与 RunMainLoop 同一顺序：先落标记，再还原输出
                _screen = null;
                screen.Uninstall();
            }
        }

        public void StartGame(Room[][] map, IsaacMazeGenerator tmg)
        {
            // 第十章「进度解锁」：按累计通关次数补背包容量 / 自带玻璃罐 / 初始 DEF·HP / 配方 1。
            // 只在新开局套用 —— 读档走 ContinueGame，那些加成已经在存档的 BaseXxx 里了。
            ProgressUnlocks.ApplyToNewRun(this);

            JoinMap(map);
            RunMainLoop(map, tmg, pendingEnter: true, unlockRecipes: true);
        }

        /// <summary>
        /// 读档进入：与 <see cref="StartGame"/> 的差别只有三点。
        ///
        /// 1. **不再解锁配方** —— `AddDefaultRecipes()` 会打印「你成功添加了合成表0!」，
        ///    读档时重跑一遍只是噪音；配方已由 `SaveRestorer` 按存档档位补过。
        /// 2. **不触发房间事件** —— `pendingEnter = false`：读档不是「走进这个房间」，
        ///    否则一读档就又打一场已经打过的战斗、再搜一遍已经搜过的空房间。
        /// 3. **站回存档时的格子**，而不是起始房间。
        /// </summary>
        public void ContinueGame(Room[][] map, IsaacMazeGenerator tmg, int row, int col)
        {
            // 直接落在存档那一格：地图是刚用种子重建的空地图（没有任何 * 标记），
            // 所以不需要「先清标记再补一个」那套麻烦事。
            JoinMap(map, row, col);

            // 派生值按「基础值 + 当前环境」重算一次（CONVENTIONS 2.3 的重算制）
            RecomputeStats();

            RunMainLoop(map, tmg, pendingEnter: false, unlockRecipes: false);
        }

        /// <summary>
        /// 主循环本体：新开局与读档共用。
        ///
        /// 两个入口的差异全部收成参数，**循环本身只有一份** ——
        /// 否则以后改循环（加楼层、加存档点）会漏掉其中一条路径。
        /// </summary>
        private void RunMainLoop(Room[][] map, IsaacMazeGenerator tmg, bool pendingEnter, bool unlockRecipes)
        {

            // 分栏界面：尺寸由地图文本决定（行数 = rows*2+1，列数算最长一行）
            ConsoleScreen screen = CreateScreenFor(tmg);
            screen.Install();
            _screen = screen;              // 供 RefreshStatus / ReadInt 使用

            // 登记「当前对局」，让退出钩子（关窗口 / Ctrl+C）能拿到玩家、地图和生成器
            SaveSession.Begin(this, map, tmg);
            Console.WriteLine("游戏开始！输入方向探索地图。");
            Console.WriteLine("方向：0上 1下 2左 3右，4 退出。");

            // 开局解锁「配方0」（内容见 AddDefaultRecipes；原先这里括注「热硫 + 玻璃罐」，
            // 早已漏掉水瓶，这次补全配方 0 后更不完整，故去掉括注、只留方法名这一处真相）。
            // 历史：这个方法原先只在一个已删除的调试入口里被调用，主游戏从来没解锁过任何配方，
            // 导致玩家选「4.徒手合成」永远只会看到「你没有任何道具合成!」。
            // 放在 Install 之后，提示才会进日志区（放前面会被 Install 的清屏抹掉）。
            // 读档不重跑（理由见 ContinueGame）。
            if (unlockRecipes)
            {
                AddDefaultRecipes();
            }
            // 这样玩家先看到自己身处何地，再看到房间里发生了什么。
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
                        if (map[PlayerRow][PlayerCol].IsHot)
                        {
                            // 高温房间必须**进房就说**（设计文档 §5.1）：它让水量消耗 +50%、
                            // 还会把背包里的木头/硫磺烤掉，不说等于让玩家白挨。
                            Console.WriteLine("【高温房间】空气滚烫，水分流失更快，木头和硫磺也会被烤。");
                        }
                        map[PlayerRow][PlayerCol].OnEnter(this);

                        // 换层后的那一次存档：等起点房的 OnEnter（开局三选一）走完再存，
                        // 否则会存下「本层开局道具还没发」的状态（见 _saveAfterEnter 的注释）。
                        if (_saveAfterEnter)
                        {
                            _saveAfterEnter = false;
                            SaveCurrentProgress(map, tmg);
                        }
                    }

                    // ---- 2.5 换层 / 通关（BossRoom 打完 Boss 时举的牌）----
                    // 放在房间事件之后、画图之前：换完图要立刻画新的那张，
                    // 而不是先画一遍旧图再画新图（会闪一下，而且旧图的房间不该再触发事件）。
                    if (GameCleared)
                    {
                        break;      // 通关：退出循环，收尾在 finally 之后处理
                    }
                    if (NextLayerRequested)
                    {
                        ClearNextLayerRequest();
                        AdvanceLayer(ref map, ref tmg, ref screen);
                        pendingEnter = true;    // 新层的起点房也要走一次 OnEnter（每层都有一次开局选道具）
                        continue;               // 立刻回到循环顶部重画新图
                    }

                    // ---- 3. 事件可能改变了状态（如水量），重画一次 ----
                    DrawScreen(screen, map, tmg);

                    ShowAvailableDirections();

                    // 地图上也能用的三项（原先只有战斗菜单里才有 —— 玩家反馈「发现只有战斗才能
                    // 装配道具 / 合成」，这是真的：OpenItems / CraftMenu / OpenAccessoryMenu
                    // 之前只挂在 OnPlayerTurn 的战斗 switch 里）。
                    // 放这里而不是让它们在别处各调一次：地图循环每回合都会回到这里，
                    // 与方向输入共用同一个 ReadLine，行为统一（输入 5/6/7 不移动，直接 continue）。
                    Console.WriteLine("5.使用道具  6.合成  7.饰品栏");

                    // 方向提示**不再塞进输入框**（输入框只画 ">"，避免提示与回显叠加导致错位）。
                    // 可走方向已经由 ShowAvailableDirections 写进日志了，这里再写一遍只会每回合刷屏；
                    // 「4 退出」这个提示改由进入地图时的开场白说明（见 RunMainLoop 开头）。

                    // 把「现在可以安全存档」的窗口打开，只包住这一次 ReadLine：
                    // 此刻房间事件已结算完、没有进行中的战斗，是整局里唯一能完整恢复的状态。
                    // 关窗口 / Ctrl+C 的钩子（ExitSaveHook）会来问这个标记。
                    SaveSession.MarkSafe(true);
                    string line = Console.ReadLine();

                    if (line == null)
                    {
                        SaveSession.MarkSafe(false);
                        _inputClosed = true;
                        Console.WriteLine("游戏结束，感谢游玩！");
                        break;
                    }

                    // ⚠️ 安全窗口一直开到**这一轮动作处理完**（用 finally 保证无论走哪条
                    //    continue/break 分支都会关掉）。原先是在 ReadLine 之后立刻关，
                    //    那样 5/6/7（改背包 / 配方 / 饰品栏）之后的改动**进不了快照** ——
                    //    玩家按 × 退出时读档会回到"按之前"的状态，看起来像刚合成的道具丢了。
                    //    包住整轮而不是只包 ReadLine，就等于「这一轮的任何改动都进快照」。
                    try
                    {
                        if (!int.TryParse(line, out int direction))
                        {
                            Console.WriteLine("输入无效，请输入数字。");
                            continue;
                        }
                        if (direction == 4)
                        {
                            // 主动退出 = 存档并退出。
                            // 为什么这里能存：方向菜单只在「房间事件全部结算完」之后才出现，
                            // 所以此刻既没有进行中的战斗、也没有半截的房间事件 ——
                            // 战斗中途的状态（敌人列表、本回合盾、回合数）没有任何恢复入口。
                            SaveCurrentProgress(map, tmg);
                            Console.WriteLine("游戏结束，感谢游玩！");
                            break;
                        }

                        // 5/6/7：地图上的「非移动动作」。不做任何一格的计算、也不触发房间事件，
                        // 做完直接回到循环顶部重画（玩家的位置没变，pendingEnter 保持 false）。
                        // 5 走的是**战斗外**那条背包路径（Player.ExplainItemsOutsideBattle）：
                        // 声明了 IOutsideBattleUsable 的一次性道具（镜子）会**真的生效并消耗**，
                        // 其余道具只得到一句「只能在战斗中使用」的解释。
                        if (direction == 5)
                        {
                            ExplainItemsOutsideBattle();
                            continue;
                        }
                        if (direction == 6)
                        {
                            CraftMenu();
                            continue;
                        }
                        if (direction == 7)
                        {
                            OpenAccessoryMenu();
                            continue;
                        }
                        // 地图上也能用调试指令（Hyberty要求）：255 开调试、129 拿道具、130 挂 buff、127 超雄、128 全配方。
                        // 与战斗里的 case 用同一套 DebugState 守卫。
                        if (direction == 255)
                        {
                            // 已经开着 debug 时再输 255：提示一句就好，**绝不能落进 Move(255)** ——
                            // `Move` 拿到越界枚举值只会回一句「无效方向！」，看起来像「指令失效了」，
                            // 而战斗菜单里输 255 是明确回「已进入 debug 模式」的，两处口径要对齐。
                            if (!DebugState.Enabled)
                            {
                                DebugState.Enable();
                                Console.WriteLine("已进入 debug 模式！");
                            }
                            else
                            {
                                Console.WriteLine("debug 模式已经开着了。");
                            }
                            DrawScreen(screen, map, tmg);   // 立刻重画：全图可见要马上生效
                            continue;
                        }
                        if (direction is 127 or 128 or 129 or 130 && DebugState.Enabled)
                        {
                            RunMapDebugCommand(direction);
                            DrawScreen(screen, map, tmg);   // 状态可能变了（STR / 配方 / 道具 / buff），立刻刷新
                            continue;
                        }

                        if (Move((Direction)direction))
                        {
                            pendingEnter = true;
                            justMoved = true;
                        }
                    }
                    finally
                    {
                        // 动作做完 → 不再是安全点。下一轮 ReadLine 之前会重新打开。
                        SaveSession.MarkSafe(false);
                    }
                }
            }
            finally
            {
                // 先把「当前对局」注销掉，再还原输出：
                // 顺序反过来的话，Uninstall 期间若恰好触发退出钩子，会拿一局已结束的游戏去存档。
                SaveSession.End();

                // 离开地图时把 Console.Out 还给真实输出，否则主菜单的输出会跑进日志区
                screen.Uninstall();
                _screen = null;
            }

            // 玩家死了就删档：这一局已经结束，留着只会让「继续游戏」把尸体读回来。
            // 注意放在 finally **之后**：死亡时循环是靠 IsAlive 退出的，没走 direction==4 那条存档分支。
            //
            // ⚠️ **先判死亡，再判通关**。反过来的话，第 10 层 Boss 战里双方同归于尽时
            //    GameCleared 和 !IsAlive 会同时成立，于是记成「通关」而不是「死亡」——
            //    玩家明明死了却拿了一次通关解锁。死亡的判定优先级应该更高。
            if (!IsAlive)
            {
                SaveFile.Delete();

                // 死亡次数记在 **profile** 里（不是 save）：save 这一局已经删了，
                // 但「通关/死亡次数」是跨对局的进度，设计文档第十章要靠它解锁东西。
                // 立刻落盘 —— 这里已经是这一局的终点，之后再没有更合适的保存点。
                Profile.AddDeath();
                Profile.Save();

                Console.WriteLine("这一局结束了。");
            }
            else if (GameCleared)
            {
                // 通关与死亡同样「这一局结束」，所以也删档 —— 区别只是记的是通关次数。
                SaveFile.Delete();
                Profile.AddClear();
                Profile.Save();
                Console.WriteLine("你通关了！这是第 " + Profile.Clears + " 次通关。");
            }
        }

        /// <summary>
        /// 换层时让玩家选下一层的环境（已解锁「可自选下一层地图」时才走这里）。
        /// 菜单里 **0 = 随机**；输入流结束（EOF）也按随机处理 —— 不能因为没输入就把换层卡死。
        ///
        /// 清单来自 <see cref="EnvironmentCatalog"/>，与开局选环境是同一份数据源
        /// （未实现的环境不在里面，否则会得到一张「没有环境」的地图）。
        /// </summary>
        private (string Name, EnvType Type) ChooseNextEnvironment(
            int layer, IReadOnlyList<(string Name, EnvType Type)> catalog)
        {
            Console.WriteLine();
            Console.WriteLine("------------ 选择第 " + layer + " 层的环境 ------------");
            for (int i = 0; i < catalog.Count; i++)
            {
                Console.WriteLine("  " + (i + 1) + ". " + catalog[i].Name);
            }
            Console.WriteLine("  0. 随机");
            Console.WriteLine("------------------------------------------");

            while (true)
            {
                int choice = ReadInt("选择环境");
                if (choice == 0)
                {
                    Console.WriteLine("随机决定。");
                    return catalog[Random.Shared.Next(catalog.Count)];
                }
                if (choice > 0 && choice <= catalog.Count)
                {
                    return catalog[choice - 1];
                }
                if (_inputClosed)
                {
                    // 没有输入了：按随机处理，别把换层卡在这里
                    return catalog[Random.Shared.Next(catalog.Count)];
                }
                Console.WriteLine("选择无效，请输入 0-" + catalog.Count);
            }
        }

        /// <summary>换层时回复的最大生命比例（Hyberty裁定：**恢复 70%**）。</summary>
        private const double NextLayerHealRatio = 0.7;

        /// <summary>
        /// 换到下一层：加层数、按新层数派生种子、重建地图与环境、把玩家挪到新图起点、存一次档。
        ///
        /// 为什么集中在这里做（而不是让 <see cref="BossRoom"/> 自己建图）：
        /// 换图要同时动地图、生成器、分栏界面三样东西，它们都在 `RunMainLoop` 手里 ——
        /// 房间只负责「举牌」（<see cref="NextLayerRequested"/>），重建由持有者统一做。
        ///
        /// 三件事的顺序不能换：
        ///   ① 先 <see cref="GameProgress.EnterNextLayer"/>（层数 +1），
        ///      ② 再用 <see cref="GameProgress.LayerSeed"/> 取新种子 —— 反了就会拿上一层的种子建新图。
        /// </summary>
        private void AdvanceLayer(ref Room[][] map, ref IsaacMazeGenerator tmg, ref ConsoleScreen screen)
        {
            GameProgress.EnterNextLayer();

            // 环境：默认**随机**；若这一层在「可自选」范围内（第十章第 1/3/5/7 次通关解锁，
            // 见 ProgressUnlocks.CanSelfSelectLayer），则让玩家自己挑 —— 菜单里 0 = 仍然随机。
            // ⚠️ 清单必须按**层数**取（EnvironmentCatalog.ForLayer）：
            //    熔岩瀑布固定第 1 层，第 2 层起永远不再出现（Hyberty裁定 2026-11-01）。
            //    这里若直接用 EnvironmentCatalog.All，自选菜单里就又能选到熔岩瀑布了。
            IReadOnlyList<(string Name, EnvType Type)> catalog = EnvironmentCatalog.ForLayer(GameProgress.Layer);
            string envName;
            EnvType nextEnvType;
            if (catalog.Count == 0)
            {
                // 兜底：清单被清空（或所有环境都还没实现）时不能 Random.Shared.Next(0)
                // —— Next(0) 返回 0，紧接着 catalog[0] 会越界。用「无环境」继续，别把换层卡死。
                (envName, nextEnvType) = ("无环境", EnvType.None);
            }
            else if (ProgressUnlocks.CanSelfSelectLayer(GameProgress.Layer))
            {
                (envName, nextEnvType) = ChooseNextEnvironment(GameProgress.Layer, catalog);
            }
            else
            {
                (envName, nextEnvType) = catalog[Random.Shared.Next(catalog.Count)];
            }

            IsaacMazeGenerator newTmg =
                new(GameProgress.MapSizeForLayer(GameProgress.Layer).Rows,
                    GameProgress.MapSizeForLayer(GameProgress.Layer).Cols,
                    nextEnvType, GameProgress.LayerSeed, GameProgress.Layer);
            newTmg.Generate();

            // 分栏界面的尺寸是按地图算的（现在都是 10×10，所以尺寸不变），
            // 但仍然按新图重建一份：screen 内部还记着敌人框 / Buff 框的状态。
            screen.Uninstall();
            map = newTmg.Grid;
            tmg = newTmg;
            screen = CreateScreenFor(tmg);
            screen.Install();
            _screen = screen;

            // 坐标回到新图起点（JoinMap 会把 StartRoom 标记为已到达；
            // 起始房间的 OnEnter 由调用方紧接着的 pendingEnter 触发）。
            JoinMap(map);

            // 换层 = 换了环境，**必须重算派生值**：新环境的 StrModifier（如熔岩瀑布缺水扣 STR）
            // 只在 RecomputeStats 里被读进去，不重算的话新层的环境加成一整层都不生效，
            // 要等到下一场战斗的 OnTurnStart 才补上（ContinueGame 里也是这么做的）。
            RecomputeStats();

            // 回血 70% 上限，不超过最大生命（Hyberty裁定）。
            // 报「实际回复量」而不是名义值：血满时会写出「你恢复了 140 点生命」但实际 +0。
            double nominalHeal = BaseHp * NextLayerHealRatio;
            double hpBefore = Hp;
            Hp = Math.Min(BaseHp, Hp + nominalHeal);
            double actualHeal = Hp - hpBefore;

            // 存档会话里存的是「当前这一局的玩家 + 地图 + 生成器」，换图后必须重新登记，
            // 否则关窗口时钩子会拿着上一层的图去存档。
            SaveSession.Begin(this, map, tmg);

            Console.WriteLine();
            Console.WriteLine("===== 第 " + GameProgress.Layer + " 层 · " + envName + " =====");
            Console.WriteLine("你恢复了 " + actualHeal + " 点生命。");

            // 不在这里 RefreshStatus()：状态栏首行用的是 _lastRoomType，此刻它还是**上一层**的房间
            // （新图刚重建、还没走 DrawScreen）。主循环紧接着就会 DrawScreen，一并刷对。
            // 提前刷一次只会让新 screen 先画出「当前房间:Boss」，下一轮再改正（闪一下）。

            // 换层要存一次（否则刚打完 Boss 的一层白打），但**不能在这里存**：
            // 新层的起点房还没走 OnEnter，`StartRoom.StartItemsGiven` 仍是 false ——
            // 此刻存档、随后进程被杀，读档会读到一个「开局三选一还没发」的状态，
            // 而读档路径的 pendingEnter 是 false，不会补发，玩家就少了一轮开局道具。
            // 所以只举一个「走完起点房就存」的牌子，由 RunMainLoop 在 OnEnter 之后执行。
            _saveAfterEnter = true;
        }

        /// <summary>
        /// 地图上执行调试指令（与战斗菜单里的 127/128/129/130 同一套行为）。
        ///
        /// 为什么抽出来：两个入口（地图方向菜单 / 战斗菜单）都调它，避免两处各写一份。
        /// 守卫在外层（`case ... when DebugState.Enabled` / `if (DebugState.Enabled)`），
        /// 这里只负责执行 —— 所以不重复判开关，否则「为什么按了没反应」会多一个来源。
        /// </summary>
        private void RunMapDebugCommand(int code)
        {
            switch (code)
            {
                case 127:
                    // 时长必须传 Buff.Permanent（-1 哨兵）：传负数会被 Math.Max(turns,0) 夹成 0，
                    // buff 挂上就判定到期、当场被清掉（这个坑在战斗里修过一次）。
                    AddBuff(new SuperMaleBuff(1, Buff.Permanent, true));
                    Console.WriteLine($"超雄模式：STR +1000（永久，跨战斗保留）。当前 STR {Str}");
                    break;
                case 128:
                    UnlockAllRecipes();
                    break;
                case 129:
                    DebugMenu.GiveItems(this);
                    break;
                case 130:
                    DebugMenu.AddBuffTo(this);
                    break;
            }
        }

        /// <summary>
        /// 存档当前进度。只在**活着、且不在战斗中**时调用（调用点是方向菜单的「4 退出」）。
        ///
        /// 这里只做「采集 + 写入」两件事，具体怎么采集在 <see cref="SaveCollector"/>，
        /// 怎么落盘在 <see cref="SaveFile"/> —— 本方法不认识 JSON、也不认识文件路径。
        /// </summary>
        private void SaveCurrentProgress(Room[][] map, IsaacMazeGenerator tmg)
        {
            if (!IsAlive || map == null)
            {
                return;
            }

            SaveData data = SaveCollector.Capture(this, map, tmg);
            if (SaveFile.Save(data))
            {
                Console.WriteLine("已保存当前进度，下次可以从主菜单「继续游戏」。");
            }
            // 失败时 SaveFile 已经打印了原因，这里不重复刷屏
        }
    }
}
