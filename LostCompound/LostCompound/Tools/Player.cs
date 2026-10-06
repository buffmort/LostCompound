using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Maps;

namespace LostCompound.Tools
{
    public class Player
    {

        private double hp = 200;
        private double MaxHp = 100;
        private double atk = 15;
        private double str = 2;
        public string name;
        private double def = 5;
        public int block = 0;
        private double DOTdmg;
        private int DOTdur;
        private int skipTurn;
        private Room[][] map;      // 当前地图
        private int playerRow;     // 玩家所在行
        private int playerCol;     // 玩家所在列
        private bool debug = false;

        public int getSkipTurn()
        {
            return skipTurn;
        }

        public void addItem(Item newItem)
        {
            if (newItem == null) return;

            if (!newItem.isStackable())
            {
                Items.Add(newItem);
                return;
            }

            // 超过单格上限的，直接删
            if (newItem.count > newItem.getMaxStack())
            {
                newItem.count = newItem.getMaxStack();
            }

            int remaining = newItem.count;

            foreach (Item existItem in Items)
            {
                if (remaining <= 0) break;
                if (existItem.getType() != newItem.getType()) continue;
                if (!existItem.isStackable()) continue;

                int space = existItem.getMaxStack() - existItem.count;
                if (space <= 0) continue;

                int move = Math.Min(space, remaining);
                existItem.count += move;
                remaining -= move;
            }

            if (remaining > 0)
            {
                newItem.count = remaining;
                Items.Add(newItem);
            }
        }
        public void setDOTdmg(int DOTdmg)
        {
            this.DOTdmg = DOTdmg;
        }
        public void addDOTdmg(double DOTdmg)
        {
            this.DOTdmg += DOTdmg;
        }

        public void addDOTdur(int DOTdur)
        {
            this.DOTdur += DOTdur;
        }

        public void setSkipTurn(int skipTurn)
        {
            this.skipTurn = skipTurn;
        }

        public void setDeath(bool death)
        {
            isDeath = death;
        }

        public double getStr()
        {
            return str;
        }

        public void setStr(double str)
        {
            this.str = str;
        }

        public int getDOTdur()
        {
            return DOTdur;
        }

        public void setDOTdur(int DOTdur)
        {
            this.DOTdur = DOTdur;
        }

        public double getDOTdmg()
        {
            return DOTdmg;
        }

        public void setDOTdmg(double DOTdmg)
        {
            this.DOTdmg = DOTdmg;
        }
        public void decreaseDOTdur() { DOTdur--; }

        public void clearDOT()
        {
            DOTdur = 0;
            DOTdmg = 0;
        }

        private bool isDeath = false;
        //玩家数组初始化
        public List<Item> Items = new List<Item>();
        public List<Item> WillAdd = new List<Item>();
        public List<Craftable> AllRecipe = new List<Craftable>();
        public void onPlayerTurn(List<Monster> monsters, EnvType envType)
        {
            if (!isAlive())
            {//判定死没死
                Console.WriteLine("你死了！");
                isDeath = true;
                return;
            }
            if (this.DOTdur > 0)
            {
                this.DOTdur -= 1;
                this.hp -= DOTdmg;
            }
            Console.WriteLine("=====你的回合=====");
            handleChangeableItems(this, envType);
            Items.AddRange(WillAdd);
            WillAdd.Clear();
            if (skipTurn > 0)
            {
                skipTurn -= 1;
                Console.WriteLine("跳过回合!");
                return;
            }
            //接下来看看堆叠 没堆叠就删除
            /*
            使用迭代器遍历玩家背包 后面还需要加入buff计算和饰品
            现在暂时不做




            * 后面进行战斗模块
            * */


            Calculator.delay(250);
            Console.WriteLine("你的血量:{0:F1}", this.hp);
            Console.WriteLine("你的防御:{0:F1}", this.def);
            Console.WriteLine("你的格挡剩余:" + this.block);
            if (DOTdur > 0)
            {
                Console.WriteLine("你剩余的dot时长:" + DOTdur);
                Console.WriteLine("你的dot伤害:" + DOTdmg);
            }
            Calculator.delay(250);
            foreach (Monster m in monsters)
            {
                Console.Write(m);
            }
            Calculator.delay(250);
            Console.WriteLine();
            Console.WriteLine("请选择操作");
            Calculator.delay(250);
            Console.WriteLine("1.攻击   2.防御   3.使用背包道具  4.徒手合成");
            if (this.debug)
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
                    block += 1;
                    break;
                case 3:
                    openItems(monsters);
                    break;
                case 4:
                    handCraft();
                    break;
                case 127:
                    Console.WriteLine("此部分尚未开发!");
                    break;
                case 128:
                    AllRecipeUnlock(); // Java 原名: AllRecipe()（C# 不允许字段与方法同名）
                    break;
                case 255:
                    this.debug = true;
                    Console.WriteLine("你已进入debug模式!");
                    break;
                default:
                    Console.WriteLine("你输入的类型不正确！ 已自动跳过回合");
                    break;
            }
        }

        public bool isAlive()
        {
            return hp > 0;
        }

        public List<Item> getItems()
        {
            return Items;
        }

        public void takeDmg(double dmg)
        {
            double realDmg = Calculator.Define(this.def, dmg);
            if (block > 0)
            {
                Console.WriteLine("你格挡了伤害!");
                block -= 1;
                return;
            }
            hp -= realDmg;
            Console.WriteLine("你受到{0:F1}点伤害!", realDmg);
            Console.WriteLine();

        }
        public void Attack(List<Monster> m)
        {
            double dmg = Calculator.Attack(this.str, this.atk);
            Console.WriteLine("=====攻击界面=====");
            Calculator.delay(250);
            int count = 0;
            foreach (Monster i in m)
            {
                Console.Write(count + "." + " " + i + "   ");
                count++;
            }
            Console.WriteLine("请选择怪物攻击:");
            int choice = int.Parse(Console.ReadLine());
            if (choice < 0 || choice > m.Count - 1)
            {
                Console.WriteLine("您的攻击无效！");
            }
            else
            {
                Monster target = m[choice];
                target.takeDmg(dmg);
                Console.WriteLine("你攻击了" + target + "!");
            }


        }
        public int selectTarget(List<Monster> m)
        {
            Console.WriteLine("请选择对象:");
            int count = 0;
            foreach (Monster i in m)
            {
                Console.Write(count + "." + " " + i + "   ");
                count++;
            }
            int choice = int.Parse(Console.ReadLine());
            if (choice < 0 || choice > m.Count - 1)
            {
                Console.WriteLine("您的投掷无效！");
                return -1;
            }
            return choice;
        }
        private void openItems(List<Monster> m)
        {
            int count = 0;
            foreach (Item i in Items)
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
            if (choice < 0 || choice > Items.Count - 1)
            {
                Console.WriteLine("您的使用无效！");
            }
            else
            {
                Item item = Items[choice];
                if (item is Useable)
                {
                    ((Useable)item).use(this, m);
                }
                else
                {
                    Console.WriteLine("您的使用无效！");

                }
            }
        }

        // 修改后的 handleChangeableItems 方法（所在类根据实际情况调整）
        private void handleChangeableItems(Player owner, EnvType envType)
        {
            List<Item> toAdd = new List<Item>();
            for (int i = 0; i < owner.Items.Count; i++)
            {
                Item item = owner.Items[i];
                if (item is TurnCount)
                {
                    // 关键修改：使用 owner 而非 this
                    ((TurnCount)item).onBattleStart(owner, envType);
                }
                if (item is Changeable)
                {
                    Changeable c = (Changeable)item;
                    if (c.ShouldRemove())
                    {
                        Item replacement = c.Become();
                        if (replacement != null)
                        {
                            toAdd.Add(replacement);
                        }
                        owner.Items.RemoveAt(i);
                        i--;
                    }
                }
            }
            foreach (Item newItem in toAdd)
            {
                owner.addItem(newItem);
            }
        }

        public int countItemById(int id)
        {
            int total = 0;
            foreach (Item item in Items)
            {
                if (item.getId() == id)
                {
                    total += item.getCount();
                }
            }
            return total;

        }

        // 修改后的 consumeItem 方法（在 Player 类中）
        public void consumeItem(int id, int amount)
        {
            // 先检查总数量是否足够
            if (countItemById(id) < amount)
            {
                Console.WriteLine("道具数量不足！");
                return;
            }
            int remaining = amount;
            for (int i = 0; i < Items.Count && remaining > 0; i++)
            {
                Item item = Items[i];
                if (item.getId() == id)
                {
                    int deduct = Math.Min(item.count, remaining);
                    item.count -= deduct;
                    remaining -= deduct;
                    if (item.count <= 0)
                    {
                        Items.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public string getItemNameById(int id)
        {
            foreach (Item item in Items)
            {
                if (item.getId() == id)
                {
                    return item.getName();
                }
            }
            return "null";
        }
        public void AllRecipeUnlock() // Java 原名: AllRecipe()（C# 不允许字段与方法同名）
        {
            if (!debug)
            {
                Console.WriteLine("你不能使用这个命令!");
                return;
            }
            AllRecipe.Add(new Recipe("热硫", new int[][]{
                    new int[] {5, 1},
                    new int[] {3, 1},
                    new int[] {1, 1}
            }, new FushionSluphur(1)));
            AllRecipe.Add(new Recipe("玻璃罐", new int[][]{
                    new int[] {5, 1},
                    new int[] {6, 1}
            }, new GlassJar(1)));
        }

        public void addRecipe0()
        {
            Console.WriteLine("你成功添加了合成表0!");
            AllRecipe.Add(new Recipe("热硫", new int[][]{
                    new int[] {5,1},
                    new int[] {3,1},
                    new int[] {1,1}
            }, new FushionSluphur(1)));
            AllRecipe.Add(new Recipe("玻璃罐", new int[][]{
                    new int[] {5,1},
                    new int[] {6,1}
            }, new GlassJar(1)));
        }

        public void handCraft()
        {
            Console.WriteLine("====徒手合成====");
            Calculator.delay(250);
            List<Craftable> available = new List<Craftable>();

            // 第一步：遍历所有配方，筛选可合成的
            foreach (Craftable recipe in AllRecipe)
            {
                if (recipe.canCraft(this))
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
                Console.WriteLine(i + ". " + r.getName() + " " + r.getMaterialsStr(this));
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
            available[choice].craft(this);
            handCraft();
        }
        /*
        * 后面是地图逻辑
        * */
        // 手动指定起始坐标进入地图
        public void enterMap(Room[][] map, int startRow, int startCol)
        {
            this.map = map;
            this.playerRow = startRow;
            this.playerCol = startCol;
            // 进入初始房间：先 enterSetup 再 onEnter
            map[playerRow][playerCol].enterSetup(this);
        }

        // 自动寻找 StartRoom 进入地图
        public void enterMap(Room[][] map)
        {
            this.map = map;
            for (int y = 0; y < map.Length; y++)
            {
                for (int x = 0; x < map[0].Length; x++)
                {
                    if (map[y][x] is StartRoom)
                    {
                        playerRow = y;
                        playerCol = x;
                        map[y][x].enterSetup(this);
                        return;
                    }
                }
            }
            throw new InvalidOperationException("地图中没有起始房间！");
        }

        public int getPlayerRow() { return playerRow; }
        public int getPlayerCol() { return playerCol; }

        public void checkDirections()
        { //检查可以走的方向
            Console.WriteLine("可行动方向：");
            // 上
            if (playerRow - 1 >= 0 &&
                    map[playerRow][playerCol].isDoor(0) &&
                    map[playerRow - 1][playerCol] != null)
            {
                Console.WriteLine("0.上");
            }
            // 下
            if (playerRow + 1 < map.Length &&
                    map[playerRow][playerCol].isDoor(1) &&
                    map[playerRow + 1][playerCol] != null)
            {
                Console.WriteLine("1.下");
            }
            // 左
            if (playerCol - 1 >= 0 &&
                    map[playerRow][playerCol].isDoor(2) &&
                    map[playerRow][playerCol - 1] != null)
            {
                Console.WriteLine("2.左");
            }
            // 右
            if (playerCol + 1 < map[0].Length &&
                    map[playerRow][playerCol].isDoor(3) &&
                    map[playerRow][playerCol + 1] != null)
            {
                Console.WriteLine("3.右");
            }
        }

        public bool move(int direction)
        {
            if (direction < 0 || direction > 3)
            {
                Console.WriteLine("无效方向！");
                return false;
            }
            Room current = map[playerRow][playerCol];
            if (!current.isDoor(direction))
            {
                Console.WriteLine("这个方向没有门！");
                return false;
            }

            int newRow = playerRow, newCol = playerCol;
            switch (direction)
            {
                case 0: newRow--; break; // 上
                case 1: newRow++; break; // 下
                case 2: newCol--; break; // 左
                case 3: newCol++; break; // 右
            }

            // 边界检查
            if (newRow < 0 || newRow >= map.Length || newCol < 0 || newCol >= map[0].Length)
            {
                Console.WriteLine("撞墙了！");
                return false;
            }

            // 离开旧房间（清除玩家标记，触发 onLeave）
            current.leaveSetup(this);

            // 更新坐标
            playerRow = newRow;
            playerCol = newCol;

            // 进入新房间（设置玩家标记，触发 onEnter）
            Room newRoom = map[playerRow][playerCol];
            newRoom.enterSetup(this);
            Console.WriteLine("你移动到了新房间。");

            // 如果 Player 持有 maze 引用，移动后打印地图
            // maze.printMap();

            return true;
        }
        // ========== 加入地图（自动寻找 StartRoom） ==========
        public void JoinMap(Room[][] map)
        {
            this.map = map;
            for (int y = 0; y < map.Length; y++)
            {
                for (int x = 0; x < map[0].Length; x++)
                {
                    if (map[y][x] is StartRoom)
                    {
                        playerRow = y;
                        playerCol = x;
                        map[y][x].enterSetup(this);
                        return;
                    }
                }
            }
            throw new InvalidOperationException("地图中没有起始房间！");
        }
        public void startGame(Room[][] map, TreeMazeGenerator tmg)
        {
            JoinMap(map);
            Console.WriteLine("游戏开始！输入方向探索地图。");

            while (this.isAlive())
            {
                // 清屏（可选）
                Console.Write("\u001b[H\u001b[2J");
                Console.WriteLine(tmg.printMapToString());
                Console.WriteLine("\n当前位置: (" + playerRow + ", " + playerCol + ")");
                Console.WriteLine("当前房间:" + map[playerRow][playerCol].getRoomType());
                checkDirections();

                int direction;
                try
                {
                    direction = int.Parse(Console.ReadLine());
                }
                catch (Exception e)
                {
                    Console.ReadLine();
                    Console.WriteLine("输入无效，请输入数字。");
                    continue;
                }
                if (direction == 4)
                {
                    Console.WriteLine("游戏结束，感谢游玩！");
                    break;
                }
                Console.Write("\u001b[H\u001b[2J");
                move(direction);
                Console.WriteLine("\n当前位置: (" + playerRow + ", " + playerCol + ")");
                Console.WriteLine("当前房间:" + map[playerRow][playerCol].getRoomType());

                // 3. 执行进入房间的逻辑（战斗、事件等）

            }
        }
    }
}
