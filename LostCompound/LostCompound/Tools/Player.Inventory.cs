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

        // 玩家背包 / 待入包 / 已解锁配方
        public List<Item> Inventory { get; } = [];
        public List<Item> PendingItems { get; } = [];

        /// <summary>
        /// 背包容量（设计文档 8.1）：初始 15 格。
        /// 一格 = 一个 Item 条目（可堆叠的道具多份共用一格）。
        /// 装不下时超出部分直接丢弃（用户决定）。
        /// </summary>
        public int InventoryCapacity { get; private set; } = 15;

        /// <summary>背包容量上限（设计文档 8.1：最终 22）。</summary>
        public const int MaxInventoryCapacity = 22;

        /// <summary>
        /// 扩充背包容量。设计文档：第 2 次通关 +3（→18），第 4 次通关 +4（→22）。
        /// 通关系统尚未实现，先留接口。
        /// </summary>
        public void GrowInventoryCapacity(int amount)
        {
            int before = InventoryCapacity;
            InventoryCapacity = Math.Min(InventoryCapacity + amount, MaxInventoryCapacity);
            if (InventoryCapacity != before)
            {
                Console.WriteLine($"背包容量扩充至 {InventoryCapacity} 格！");
            }
        }

        public void AddItem(Item newItem)
        {
            if (newItem == null) return;

            if (!newItem.IsStackable)
            {
                // 不可堆叠 = 独占一格。背包满了就丢弃（按用户决定：超过了直接删除）
                if (Inventory.Count >= InventoryCapacity)
                {
                    Console.WriteLine($"背包已满，{newItem.Name} x{newItem.Count} 被丢弃了！");
                    return;
                }
                Inventory.Add(newItem);
                Console.WriteLine($"你获得了 {newItem.Name} x{newItem.Count}！");
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
                // 现有堆叠都满了，需要新开一格；背包满则丢弃剩余部分
                if (Inventory.Count >= InventoryCapacity)
                {
                    Console.WriteLine($"背包已满，{newItem.Name} x{remaining} 被丢弃了！");
                }
                else
                {
                    newItem.Count = remaining;
                    Inventory.Add(newItem);
                    added += remaining;
                }
            }

            // 统一在这里播报获得，调用方不要再重复打印
            if (added > 0)
            {
                Console.WriteLine($"你获得了 {newItem.Name} x{added}！");
            }
        }

        private void OpenItems(List<Monster> monsters)
        {
            int count = 0;
            foreach (Item i in Inventory)
            {
                if (i is IUseable)
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

            if (Inventory.Count == 0)
            {
                // 空背包没有任何合法编号，重问会死循环
                Console.WriteLine("你的背包空空如也！");
                return;
            }

            // 选错道具不再结束回合：重问直到选到合法编号为止
            int choice;
            while (true)
            {
                Console.WriteLine("请选择道具使用");
                choice = ReadInt("选择道具编号(-1即退出)");
                if (choice == -1)
                {
                    // 直接返回。原来是 break，但那样 choice 仍是 -1，
                    // 紧接着的 Inventory[choice] 就会越界访问。
                    // 输入流结束（EOF）也走这条：同样返回、不消耗回合，不会空转。
                    return;
                }
                if (choice >= 0 && choice < Inventory.Count) break;
                Console.WriteLine("您的使用无效！请重新选择(0-" + (Inventory.Count - 1) + "):");
            }

            Item item = Inventory[choice];
            if (item is IUseable useable)
            {
                useable.Use(this, monsters);

                // 用完立刻收尸：把「数量归零 / 该转变」的道具当场处理掉，别再等下一个玩家回合。
                //
                // 为什么必须在这里做：IUseable.Use() 只负责扣 Count（如硫酸 / 水瓶 / 金属 / 碘片），
                // 而移除的时机原先**只**挂在 OnTurnStart → HandleChangeableItems 上。
                // 于是「用掉最后一瓶水」之后，本回合剩下的所有显示（敌人框、状态栏、日志）
                // 都还在背包里挂着一条「水瓶 x0」—— 直到下一回合开头才消失。
                // 那不是显示延迟 0.5 秒的问题：道具列表是玩家下一次 OpenItems 要按编号选的，
                // x0 的条目占着一个编号、还能被再次选中，点下去只会得到一句「你已经没有…」。
                //
                // 为什么判定必须用 ShouldRemove()：不能写 `if (item.Count == 0)`。
                // IChangeable 的语义是「数量归零后移出背包 / 变成别的」（CONVENTIONS 2.4），
                // 具体条件由各道具自己声明（ShouldRemove），Become 决定是否留下替代品。
                // 把条件写死成「Count == 0」会漏掉「还没用光但应该移除」的道具
                // （如将来某个道具在耐久耗尽时就该消失），所以这里只调用 c.ShouldRemove()，
                // 与回合末那条通路**走同一个判定**，绝不在使用路径上另立一套规则。
                //
                // 注意：走到这里意味着上面的 while 已经校验过编号合法，且 branch 确实执行了 Use()。
                // 用户输入无效编号 / -1 退出 / 非 IUseable 时都不会到这里，不会被误删。
                RemoveConsumedItems();
            }
            else
            {
                Console.WriteLine("您的使用无效！");
            }
        }

        /// <summary>
        /// 每个玩家回合结算一次「背包里所有与回合数有关的道具」：
        /// ITurnCount（如木头在熔岩瀑布烧成碳）先推进计数器，
        /// 再立刻判断 IChangeable 是否该转变/移除，需要转变的产物统一在遍历结束后入包，
        /// 避免边遍历边修改 Inventory。
        ///
        /// ⚠️ 「推进计数器」与「收尸」必须**逐项交替**（先推进、再收这一项），
        /// 不能拆成「全推进一轮、再收尸一轮」：某个道具的计数推进可能正好把自己用到 0
        /// （如木头按环境扣 Change），那种道具应当在**本轮**就被收走，而拆成两轮会让它多留一轮。
        /// 这里只把收尸那一步抽成 <see cref="TryRemoveIfConsumed"/>（与「用完立刻收尸」共用判定），
        /// 交替顺序逐字保留。
        /// </summary>
        private void HandleChangeableItems(EnvType envType)
        {
            List<Item> toAdd = [];
            for (int i = 0; i < Inventory.Count; i++)
            {
                Item item = Inventory[i];
                if (item is ITurnCount turnCount)
                {
                    turnCount.OnBattleStart(this, envType);
                }
                // 收尸后 i-- 让本次循环重看同一个下标（列表左移了一格）—— 与原先一致
                if (TryRemoveIfConsumed(Inventory, i, toAdd)) i--;
            }
            foreach (Item newItem in toAdd)
            {
                AddItem(newItem);
            }
        }

        /// <summary>
        /// 若 <paramref name="list"/> 的 <paramref name="index"/> 处道具满足
        /// <see cref="IChangeable.ShouldRemove"/>（「数量归零后移出 / 变成别的」，
        /// 判定归各道具自己声明），就把它移出该列表，
        /// 产物（<see cref="IChangeable.Become"/>）收进 <paramref name="toAdd"/> 由调用方统一入包。
        /// 返回是否发生了移除（调用方据此决定要不要 `i--`）。
        ///
        /// 为什么收列表参数而不是写死 Inventory：收尸通路后来多了一条
        /// （饰品栏里的斗篷破损，见 <see cref="RemoveBrokenEquippedItems"/>），
        /// 而「怎么判断该走、走到哪去」只应该有一处实现 —— 抄第二份早晚会分叉。
        ///
        /// 为什么判定必须走 ShouldRemove()：不能写 `if (item.Count == 0)` —— 那会漏掉
        /// 「还没用光但应该移除」的道具，等于在使用路径上另立一套与回合末不同的规则。
        /// 三条收尸通路（回合末 / 用完一次 / 装备在受伤通知里用坏自己）走同一个判定，
        /// 正是因为收尸条件只有一处定义。
        /// </summary>
        private static bool TryRemoveIfConsumed(List<Item> list, int index, List<Item> toAdd)
        {
            Item item = list[index];
            if (item is not IChangeable c || !c.ShouldRemove()) return false;

            Item replacement = c.Become();
            if (replacement != null)
            {
                toAdd.Add(replacement);
            }
            list.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 扫一个道具列表，收走所有满足 <see cref="IChangeable.ShouldRemove"/> 的道具。
        /// 产物（<c>Become()</c>）只收集、不入包 —— 由调用方在遍历结束后统一处理，
        /// 免得边遍历边改列表。返回是否收过东西（调用方据此决定要不要重算派生值）。
        /// </summary>
        private static bool SweepConsumed(List<Item> list, List<Item> toAdd)
        {
            bool removed = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (TryRemoveIfConsumed(list, i, toAdd))
                {
                    removed = true;
                    i--;   // 列表左移了一格，重看同一个下标
                }
            }
            return removed;
        }

        /// <summary>
        /// 收走背包里所有「该走了」的道具 —— 用完道具（<see cref="OpenItems"/>）后立刻调用，
        /// 免得「用掉最后一瓶水」之后背包里还挂着一条可被重新选中的「水瓶 x0」，
        /// 一直等到下一个玩家回合的 <see cref="HandleChangeableItems"/> 才消失。
        ///
        /// 只做收尸，不推进 ITurnCount —— 那是「回合」的事，不是「用了一次」的事。
        /// </summary>
        private void RemoveConsumedItems()
        {
            List<Item> toAdd = [];
            SweepConsumed(Inventory, toAdd);
            foreach (Item newItem in toAdd)
            {
                AddItem(newItem);
            }
        }

        public int CountItemById(int id) => Inventory.Where(item => item.Id == id).Sum(item => item.Count);

        /// <summary>
        /// 从背包扣除指定 ID 的若干个道具（跨多个堆叠扣），数量归零的堆叠直接移除。
        /// 调用前先整体校验总量，避免扣到一半发现不够、留下「扣了一半」的状态。
        /// </summary>
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
    }
}
