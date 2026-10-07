using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Maps;

namespace LostCompound.Tools
{
    public class Player
    {

        public double Hp { get; set; } = 200;
        public double Atk { get; set; } = 15;
        public double Str { get; set; } = 2;
        public string Name { get; set; }
        public double Def { get; set; } = 5;
        public int Block { get; set; }
        public double DotDamage { get; set; }
        public int DotDuration { get; set; }
        public int SkipTurn { get; set; }
        public bool IsDead { get; set; }
        private Room[][] _map;      // 当前地图
        public int PlayerRow { get; private set; }     // 玩家所在行
        public int PlayerCol { get; private set; }     // 玩家所在列
        private bool _debug;

        // 玩家背包 / 待入包 / 已解锁配方
        public List<Item> Inventory { get; } = [];
        public List<Item> PendingItems { get; } = [];
        public List<Craftable> Recipes { get; } = [];

        // 存活判定：派生值用计算属性（原 isAlive() 方法）
        public bool IsAlive => Hp > 0;

        // 注意：原 addDOTdur 做的是 DOTdur += v（累加），不能写成属性，否则会变成覆盖。
        public void AddDotDuration(int duration) => DotDuration += duration;

        // 注意：原 addDOTdmg 做的是 DOTdmg += v（累加），同样不能写成属性。
        public void AddDotDamage(double damage) => DotDamage += damage;

        public void AddItem(Item newItem)
        {
            if (newItem == null) return;

            if (!newItem.IsStackable)
            {
                Inventory.Add(newItem);
                Console.WriteLine("你获得了 " + newItem.Name + " x" + newItem.Count + "！");
                return;
            }

            // 超过单格上限的，直接删
            if (newItem.Count > newItem.MaxStack)
            {
                newItem.Count = newItem.MaxStack;
            }

            int remaining = newItem.Count;
            int added = 0;   // 实际加进背包的数量（堆叠时可能小于 original count）

            foreach (Item existItem in Inventory)
            {
                if (remaining <= 0) break;
                if (existItem.Type != newItem.Type) continue;
                if (!existItem.IsStackable) continue;

                int space = existItem.MaxStack - existItem.Count;
                if (space <= 0) continue;

                int move = Math.Min(space, remaining);
                existItem.Count += move;
                remaining -= move;
                added += move;
            }

            if (remaining > 0)
            {
                newItem.Count = remaining;
                Inventory.Add(newItem);
                added += remaining;
            }

            // 统一在这里播报获得，调用方不要再重复打印
            if (added > 0)
            {
                Console.WriteLine("你获得了 " + newItem.Name + " x" + added + "！");
            }
        }

        public void DecreaseDotDuration() { DotDuration--; }

        public void ClearDot()
        {
            DotDuration = 0;
            DotDamage = 0;
        }

        public void OnPlayerTurn(List<Monster> monsters, EnvType envType)
        {
            if (!IsAlive)
            {//判定死没死
                Console.WriteLine("你死了！");
                IsDead = true;
                return;
            }
            if (DotDuration > 0)
            {
                DotDuration -= 1;
                Hp -= DotDamage;
            }
            Console.WriteLine("=====你的回合=====");
            HandleChangeableItems(this, envType);
            Inventory.AddRange(PendingItems);
            PendingItems.Clear();
            if (SkipTurn > 0)
            {
                SkipTurn -= 1;
                Console.WriteLine("跳过回合!");
                return;
            }
            //接下来看看堆叠 没堆叠就删除
            /*
            使用迭代器遍历玩家背包 后面还需要加入buff计算和饰品
            现在暂时不做




            * 后面进行战斗模块
            * */


            Calculator.Delay(250);
            Console.WriteLine("你的血量:{0:F1}", Hp);
            Console.WriteLine("你的防御:{0:F1}", Def);
            Console.WriteLine("你的格挡剩余:" + Block);
            // 环境状态（如熔岩瀑布水量）：供玩家决定要不要用水瓶
            if (CurrentEnvironment != null)
            {
                Console.WriteLine(CurrentEnvironment.DescribeStatus());
            }
            if (DotDuration > 0)
            {
                Console.WriteLine("你剩余的dot时长:" + DotDuration);
                Console.WriteLine("你的dot伤害:" + DotDamage);
            }
            Calculator.Delay(250);
            foreach (Monster m in monsters)
            {
                Console.Write(m);
            }
            Calculator.Delay(250);
            Console.WriteLine();
            Console.WriteLine("请选择操作");
            Calculator.Delay(250);
            Console.WriteLine("1.攻击   2.防御   3.使用背包道具  4.徒手合成");
            if (_debug)
            {
                Console.WriteLine("debug模式操作:");
                Console.WriteLine("127, 获得全部合成");
            }
            int option = int.Parse(Console.ReadLine());
            switch (option)
            {
                case 1:
                    Attack(monsters);
                    break;
                case 2:
                    Block += 1;
                    break;
                case 3:
                    OpenItems(monsters);
                    break;
                case 4:
                    HandCraft();
                    break;
                case 127:
                    Console.WriteLine("此部分尚未开发!");
                    break;
                case 128:
                    UnlockAllRecipes();
                    break;
                case 255:
                    _debug = true;
                    Console.WriteLine("你已进入debug模式!");
                    break;
                default:
                    Console.WriteLine("你输入的类型不正确！ 已自动跳过回合");
                    break;
            }
        }

        public void TakeDamage(double dmg)
        {
            double realDmg = Calculator.ApplyDefense(Def, dmg);
            if (Block > 0)
            {
                Console.WriteLine("你格挡了伤害!");
                Block -= 1;
                return;
            }
            Hp -= realDmg;
            Console.WriteLine("你受到{0:F1}点伤害!", realDmg);
            Console.WriteLine();

        }

        /// <summary>
        /// 玩家当前所在房间的环境对象（可能为 null：没进地图、或该层没有环境）。
        /// 用 internal 而不是 public：只给同一程序集内的道具（水瓶）读取，不扩大对外公开 API。
        /// </summary>
        internal Environment CurrentEnvironment
        {
            get
            {
                if (_map == null) return null;
                Room room = _map[PlayerRow][PlayerCol];
                return room?.Environment;
            }
        }

        /// <summary>当前有效 STR = 基础 STR + 环境修正（如熔岩瀑布缺水会削弱）。</summary>
        private double EffectiveStr
        {
            get
            {
                Environment env = CurrentEnvironment;
                // 注意符号：StrModifier 在缺水时是负值，所以这里直接「加」修正量即可实现
                // 「缺水 → STR 变小」；水满时修正量为 0，与原来完全一致。
                return Str + (env?.StrModifier ?? 0.0);
            }
        }

        public void Attack(List<Monster> monsters)
        {
            double dmg = Calculator.CalculateAttack(EffectiveStr, Atk);
            Console.WriteLine("=====攻击界面=====");
            Calculator.Delay(250);
            int count = 0;
            foreach (Monster i in monsters)
            {
                Console.Write(count + "." + " " + i + "   ");
                count++;
            }
            Console.WriteLine("请选择怪物攻击:");
            int choice = int.Parse(Console.ReadLine());
            if (choice < 0 || choice > monsters.Count - 1)
            {
                Console.WriteLine("您的攻击无效！");
            }
            else
            {
                Monster target = monsters[choice];
                target.TakeDamage(dmg);
                Console.WriteLine("你攻击了" + target + "!");
            }


        }

        public int SelectTarget(List<Monster> monsters)
        {
            Console.WriteLine("请选择对象:");
            int count = 0;
            foreach (Monster i in monsters)
            {
                Console.Write(count + "." + " " + i + "   ");
                count++;
            }
            int choice = int.Parse(Console.ReadLine());
            if (choice < 0 || choice > monsters.Count - 1)
            {
                Console.WriteLine("您的投掷无效！");
                return -1;
            }
            return choice;
        }

        private void OpenItems(List<Monster> monsters)
        {
            int count = 0;
            foreach (Item i in Inventory)
            {
                if (i is Useable)
                {
                    Console.WriteLine(count + "." + i + "[可使用]");
                    count++;
                }
                else
                {
                    Console.WriteLine(count + "." + i);
                    count++;
                }
            }
            Console.WriteLine("请选择道具使用");
            int choice = int.Parse(Console.ReadLine());
            if (choice < 0 || choice > Inventory.Count - 1)
            {
                Console.WriteLine("您的使用无效！");
            }
            else
            {
                Item item = Inventory[choice];
                if (item is Useable useable)
                {
                    useable.Use(this, monsters);
                }
                else
                {
                    Console.WriteLine("您的使用无效！");

                }
            }
        }

        // 修改后的 HandleChangeableItems 方法（所在类根据实际情况调整）
        private void HandleChangeableItems(Player owner, EnvType envType)
        {
            List<Item> toAdd = [];
            for (int i = 0; i < owner.Inventory.Count; i++)
            {
                Item item = owner.Inventory[i];
                if (item is TurnCount turnCount)
                {
                    // 关键修改：使用 owner 而非 this
                    turnCount.OnBattleStart(owner, envType);
                }
                if (item is Changeable c)
                {
                    if (c.ShouldRemove())
                    {
                        Item replacement = c.Become();
                        if (replacement != null)
                        {
                            toAdd.Add(replacement);
                        }
                        owner.Inventory.RemoveAt(i);
                        i--;
                    }
                }
            }
            foreach (Item newItem in toAdd)
            {
                owner.AddItem(newItem);
            }
        }

        public int CountItemById(int id)
        {
            int total = 0;
            foreach (Item item in Inventory)
            {
                if (item.Id == id)
                {
                    total += item.Count;
                }
            }
            return total;

        }

        // 修改后的 ConsumeItem 方法（在 Player 类中）
        public void ConsumeItem(int id, int amount)
        {
            // 先检查总数量是否足够
            if (CountItemById(id) < amount)
            {
                Console.WriteLine("道具数量不足！");
                return;
            }
            int remaining = amount;
            for (int i = 0; i < Inventory.Count && remaining > 0; i++)
            {
                Item item = Inventory[i];
                if (item.Id == id)
                {
                    int deduct = Math.Min(item.Count, remaining);
                    item.Count -= deduct;
                    remaining -= deduct;
                    if (item.Count <= 0)
                    {
                        Inventory.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public string GetItemNameById(int id)
        {
            foreach (Item item in Inventory)
            {
                if (item.Id == id)
                {
                    return item.Name;
                }
            }
            return "null";
        }

        public void UnlockAllRecipes()
        {
            if (!_debug)
            {
                Console.WriteLine("你不能使用这个命令!");
                return;
            }
            Recipes.Add(new Recipe("热硫",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Charcoal, 1], [ItemIds.Sulphur, 1]],
                new FushionSluphur(1)));
            Recipes.Add(new Recipe("玻璃罐",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Sand, 1]],
                new GlassJar(1)));
        }

        public void AddDefaultRecipes()
        {
            Console.WriteLine("你成功添加了合成表0!");
            Recipes.Add(new Recipe("热硫",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Charcoal, 1], [ItemIds.Sulphur, 1]],
                new FushionSluphur(1)));
            Recipes.Add(new Recipe("玻璃罐",
                [[ItemIds.WhitePhosphorus, 1], [ItemIds.Sand, 1]],
                new GlassJar(1)));
        }

        public void HandCraft()
        {
            Console.WriteLine("====徒手合成====");
            Calculator.Delay(250);
            List<Craftable> available = [];

            // 第一步：遍历所有配方，筛选可合成的
            foreach (Craftable recipe in Recipes)
            {
                if (recipe.CanCraft(this))
                {
                    available.Add(recipe);
                }
            }

            // 第二步：检查是否有可合成的
            if (available.Count == 0)
            {
                Console.WriteLine("你没有任何道具合成!");
                return;
            }

            // 第三步：展示可合成列表
            for (int i = 0; i < available.Count; i++)
            {
                Craftable r = available[i];
                Console.WriteLine(i + ". " + r.Name + " " + r.DescribeMaterials(this));
            }

            // 第四步：获取选择并执行
            Console.WriteLine("255. 返回");
            Console.WriteLine("请选择..");
            int choice = int.Parse(Console.ReadLine());
            if (choice == 255) return;
            if (choice < 0 || choice >= available.Count)
            {
                Console.WriteLine("选择无效!");
                return;
            }
            available[choice].Craft(this);
            HandCraft();
        }
        /*
        * 后面是地图逻辑
        * */
        // 手动指定起始坐标进入地图
        // 注意：这里只定位并标记到达，不触发 OnEnter；房间事件由调用方（StartGame）在展示地图后触发。
        public void EnterMap(Room[][] map, int startRow, int startCol)
        {
            _map = map;
            PlayerRow = startRow;
            PlayerCol = startCol;
            map[PlayerRow][PlayerCol].MarkArrived();
        }

        // 自动寻找 StartRoom 进入地图
        public void EnterMap(Room[][] map)
        {
            _map = map;
            for (int y = 0; y < map.Length; y++)
            {
                for (int x = 0; x < map[0].Length; x++)
                {
                    if (map[y][x] is StartRoom)
                    {
                        PlayerRow = y;
                        PlayerCol = x;
                        map[y][x].MarkArrived();
                        return;
                    }
                }
            }
            throw new InvalidOperationException("地图中没有起始房间！");
        }

        public void ShowAvailableDirections()
        { //检查可以走的方向
            Console.WriteLine("可行动方向：");
            // 上
            if (PlayerRow - 1 >= 0 &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Up) &&
                    _map[PlayerRow - 1][PlayerCol] != null)
            {
                Console.WriteLine("0.上");
            }
            // 下
            if (PlayerRow + 1 < _map.Length &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Down) &&
                    _map[PlayerRow + 1][PlayerCol] != null)
            {
                Console.WriteLine("1.下");
            }
            // 左
            if (PlayerCol - 1 >= 0 &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Left) &&
                    _map[PlayerRow][PlayerCol - 1] != null)
            {
                Console.WriteLine("2.左");
            }
            // 右
            if (PlayerCol + 1 < _map[0].Length &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Right) &&
                    _map[PlayerRow][PlayerCol + 1] != null)
            {
                Console.WriteLine("3.右");
            }
        }

        public bool Move(Direction direction)
        {
            if (!Enum.IsDefined(direction))
            {
                Console.WriteLine("无效方向！");
                return false;
            }
            Room current = _map[PlayerRow][PlayerCol];
            if (!current.HasDoor(direction))
            {
                Console.WriteLine("这个方向没有门！");
                return false;
            }

            int newRow = PlayerRow, newCol = PlayerCol;
            switch (direction)
            {
                case Direction.Up: newRow--; break; // 上
                case Direction.Down: newRow++; break; // 下
                case Direction.Left: newCol--; break; // 左
                case Direction.Right: newCol++; break; // 右
            }

            // 边界检查
            if (newRow < 0 || newRow >= _map.Length || newCol < 0 || newCol >= _map[0].Length)
            {
                Console.WriteLine("撞墙了！");
                return false;
            }

            // 离开旧房间（清除玩家标记，触发 OnLeave）
            current.LeaveSetup(this);

            // 更新坐标
            PlayerRow = newRow;
            PlayerCol = newCol;

            // 只标记「玩家已到达」，不在这里触发 OnEnter。
            // 事件改由 StartGame 在打印地图之后调用，输出顺序变为：地图 → 房间类型 → 房间事件。
            _map[PlayerRow][PlayerCol].MarkArrived();

            return true;
        }
        // ========== 加入地图（自动寻找 StartRoom） ==========
        public void JoinMap(Room[][] map)
        {
            _map = map;
            for (int y = 0; y < map.Length; y++)
            {
                for (int x = 0; x < map[0].Length; x++)
                {
                    if (map[y][x] is StartRoom)
                    {
                        PlayerRow = y;
                        PlayerCol = x;
                        // 只标记到达；起始房间的事件由 StartGame 在打印第一张地图后触发
                        map[y][x].MarkArrived();
                        return;
                    }
                }
            }
            throw new InvalidOperationException("地图中没有起始房间！");
        }

        /// <summary>清屏并画出「地图 → 当前位置 → 当前房间」这一段状态。</summary>
        private void DrawStatus(Room[][] map, TreeMazeGenerator tmg)
        {
            Console.Write("\u001b[H\u001b[2J");
            Console.WriteLine(tmg.ToDisplayString());
            Console.WriteLine("\n当前位置: (" + PlayerRow + ", " + PlayerCol + ")");
            Console.WriteLine("当前房间:" + map[PlayerRow][PlayerCol].Type);
        }

        public void StartGame(Room[][] map, TreeMazeGenerator tmg)
        {
            JoinMap(map);
            Console.WriteLine("游戏开始！输入方向探索地图。");

            // 房间事件不立即触发，而是等地图打印之后再触发，
            // 这样玩家先看到自己身处何地，再看到房间里发生了什么。
            bool pendingEnter = true;    // 起始房间的事件也要等第一张地图
            bool justMoved = false;
            bool keepScreen = false;     // 上次操作失败（无门/撞墙/输入非法）时不清屏，让提示留在屏幕上

            while (IsAlive)
            {
                if (keepScreen)
                {
                    // 关键：不清屏。否则「这个方向没有门！」这类提示会被立刻抹掉，玩家根本看不到。
                    keepScreen = false;
                    Console.WriteLine();
                }
                else
                {
                    // ---- 1. 先发送地图与位置/房间类型 ----
                    DrawStatus(map, tmg);
                    if (justMoved)
                    {
                        Console.WriteLine("你移动到了新房间。");
                        justMoved = false;
                    }

                    // ---- 2. 再触发房间事件（宝箱、战斗等）----
                    bool entered = pendingEnter;
                    if (entered)
                    {
                        pendingEnter = false;
                        map[PlayerRow][PlayerCol].OnEnter(this);
                    }

                    // ---- 3. 事件（尤其战斗日志）会输出大量内容把地图顶出屏幕，重画一次 ----
                    if (entered)
                    {
                        DrawStatus(map, tmg);
                    }
                }

                ShowAvailableDirections();

                int direction;
                try
                {
                    direction = int.Parse(Console.ReadLine());
                }
                catch (Exception)
                {
                    // Java 原版这里是 scanner.nextLine()，用于丢弃 nextInt() 失败后残留的输入。
                    // C# 的 Console.ReadLine() 已经把整行读走了，再读一次会吞掉玩家的下一次输入，故删除。
                    Console.WriteLine("输入无效，请输入数字。");
                    keepScreen = true;
                    continue;
                }
                if (direction == 4)
                {
                    Console.WriteLine("游戏结束，感谢游玩！");
                    break;
                }

                // 移动成功后，下一轮循环会先打印地图，再触发新房间的事件
                if (Move((Direction)direction))
                {
                    pendingEnter = true;
                    justMoved = true;
                }
                else
                {
                    keepScreen = true;   // 移动失败：保留错误提示，下一轮不清屏
                }
            }
        }
    }
}
