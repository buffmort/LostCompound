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

namespace LostCompound.Tools.Players
{
    public partial class Player
    {

        // 玩家背包 / 待入包 / 已解锁配方
        public List<Item> Inventory { get; } = [];
        public List<Item> PendingItems { get; } = [];

        /// <summary>
        /// 背包容量（设计文档 8.1）：初始 15 格。
        /// 一格 = 一个 Item 条目（可堆叠的道具多份共用一格）。
        /// 装不下时超出部分直接丢弃（Hyberty决定）。
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
                // 不可堆叠 = 独占一格。背包满了就丢弃（按Hyberty决定：超过了直接删除）
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

        /// <summary>
        /// **不在战斗中**时打开背包（地图上按 5）。
        ///
        /// 两种结局，按道具自己声明来分：
        ///   · 实现了 <see cref="IOutsideBattleUsable"/> 的（目前只有一次性的镜子）——
        ///     **真的执行** <see cref="IUseable.Use"/>，用完照常收尸；
        ///   · 其余 <see cref="IUseable"/> —— 只调 <see cref="IUseable.ExplainBattleOnly"/>，
        ///     **不执行、不消耗**。因为大部分道具是 AOE / 需要目标 / 需要水源的，
        ///     在没有敌人的房间里硬执行会很荒谬（毒气弹白白消耗、水瓶在没有水源的地方"回满水量"）。
        ///
        /// 这个区分就是 <see cref="IOutsideBattleUsable"/> 存在的理由：
        /// 「战斗外能不能用」由道具自己声明，调用方不认识具体道具类。
        ///
        /// 为什么不在 OpenItems 里加一个 bool 参数：那样两套流程会绞在一起
        /// （列表要不要标 [可使用]、选完要不要收尸、要不要弹战斗提示…），
        /// 而这边的主线是「解释」。分成两个方法，各自读起来就是它该做的事。
        /// </summary>
        private void ExplainItemsOutsideBattle()
        {
            if (Inventory.Count == 0)
            {
                Console.WriteLine("你的背包空空如也！");
                return;
            }

            Console.WriteLine("=====背包（战斗外）=====");
            for (int i = 0; i < Inventory.Count; i++)
            {
                // 标一下「选它会真的生效」，否则玩家分不清「执行」和「得到一句解释」。
                bool usable = Inventory[i] is IUseable;
                bool outside = Inventory[i] is IOutsideBattleUsable;
                Console.WriteLine(i + "." + Inventory[i] + (outside ? "[可用]" : usable ? "[仅战斗中]" : ""));
            }
            Console.WriteLine("-1. 返回");

            while (true)
            {
                int choice = ReadInt("选择道具");
                if (choice == -1)
                {
                    // 两种来源都走这里：玩家自己输 -1（返回），或输入流结束（ReadInt 置了
                    // _inputClosed 并返回 -1）。后者不需要另判 —— 再问一遍也只会立刻返回 -1。
                    return;
                }
                if (choice < 0 || choice >= Inventory.Count)
                {
                    Console.WriteLine("选择无效，请输入 0-" + (Inventory.Count - 1) + "（-1 返回）");
                    continue;
                }

                Item item = Inventory[choice];
                if (item is IOutsideBattleUsable && item is IUseable outsideUseable)
                {
                    // 这件道具声明了「战斗外也能用」（如一次性的镜子）：
                    // 真的执行效果，并像战斗路径一样立刻收尸（用完即从背包消失）。
                    // 传空列表 = 场上没有敌人；实现方必须做到不依赖怪物列表
                    // （接口注释里写明了这条义务）。
                    outsideUseable.Use(this, []);
                    RemoveConsumedItems();
                    RefreshAfterItemUse();
                    return;
                }
                if (item is IUseable useable)
                {
                    // 其余道具自己解释「为什么现在不能用」（文案在各自类里，这里不认识具体道具）
                    useable.ExplainBattleOnly(this);
                }
                else
                {
                    Console.WriteLine(item.Name + " 不能直接使用。");
                }
                return;
            }
        }

        private void OpenItems(List<Monster> monsters)
        {
            // 「搅乱泥沙」这类 debuff 会封住背包（Buff.BlocksInventory）。
            // 口径：**尝试一次就消费一次** —— 挡下来的这一下算它生效，之后 buff 消失
            // （1 回合的效果因此在玩家真正动手时兑现，而不是在大回合开场就白掉，
            //  见 MuddyWater 的类注释）。
            for (int i = 0; i < Buffs.Count; i++)
            {
                if (!Buffs[i].BlocksInventory) continue;

                Console.WriteLine("泥沙糊住了你的背包，这一回合打不开！");
                Buffs.RemoveAt(i);
                RefreshBuffBox();
                return;
            }

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
                // **不在战斗中**（地图上按 5 使用道具）时 monsters 传的是 null。
                // 这里换成空列表再交给道具：毒气弹 / 红磷 / 白磷 / 压缩沙瓶 都是
                // `foreach (Monster x in m)`，对 null 会直接 NullReferenceException。
                // 换成空表之后它们会「什么也没打中」地正常走完 —— 与它们的语义一致：
                // 房间里没有敌人，AOE 自然落空（道具照样消耗，这也是文档对 AOE 的口径）。
                useable.Use(this, monsters ?? []);

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
                // Hyberty输入无效编号 / -1 退出 / 非 IUseable 时都不会到这里，不会被误删。
                RemoveConsumedItems();

                // 再刷一次界面：Use 可能直接改了玩家状态（镜子的 Base 对换 / Hp 夹取），
                // 不刷的话状态框要等到下次挨打或下一回合才更新（见 RefreshAfterItemUse 的注释）。
                RefreshAfterItemUse();
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
        /// 收走背包里所有「该走了」的道具 —— 两个使用入口（<see cref="OpenItems"/> 战斗内、
        /// <see cref="ExplainItemsOutsideBattle"/> 战斗外用掉一次性道具）之后都会调用它，
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
        /// 镜子(45) 的效果②：**背包里各道具的数量整体轮换一格**。
        ///
        /// 规则（Hyberty原话）：「记录当前道具位置 将第 n 个道具数量变成第 n+1 个道具数量，
        /// 最后一个与第一个交换（0→1→2→3…）」。
        ///
        /// 两个必须的细节：
        ///   ① **先拍快照再写**。直接一边读一边写会把同一个数量连续搬好几格
        ///      （第 1 格拿到第 2 格、第 2 格又拿到"已经被改过的第 3 格"之类），
        ///      结果不是轮换而是全变成同一个数。快照 = Hyberty说的「记录当前道具位置」。
        ///   ② **超过堆叠上限就夹到上限**（Hyberty裁定）。这样非堆叠道具
        ///      （`MaxStack` = 1，`Count` 恒为 1 的不变量，见 CONVENTIONS 2.4）
        ///      不会被赋成 20 这种值 —— 夹一下就不变量也不会破。
        ///
        /// 空背包 / 只有一个道具时轮换没有意义，直接返回（避免下面取模的边界讨论）。
        /// 镜子自己在调用前已经被移除（Hyberty裁定「镜子先移除再轮」），所以它不占位。
        /// </summary>
        public void RotateInventoryCounts()
        {
            int n = Inventory.Count;
            if (n < 2)
            {
                return;
            }

            int[] snapshot = new int[n];
            for (int i = 0; i < n; i++)
            {
                snapshot[i] = Inventory[i].Count;
            }

            for (int i = 0; i < n; i++)
            {
                int next = snapshot[(i + 1) % n];       // 最后一格取第 0 格 → 首尾相连
                Inventory[i].Count = Math.Min(next, Inventory[i].MaxStack);
            }
        }

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

        /// <summary>
        /// 按道具 ID 取**显示名**（配方材料列表用的就是它）。
        ///
        /// 查找顺序：**背包里的实例** → <see cref="ItemFactory"/> 的表 → 「未知道具(ID n)」。
        ///
        /// ⚠️ 为什么必须有第二步（这是一个真 bug 的修复）：
        ///   原先只查背包，查不到就 `return "null"` —— 于是一份配方只要材料还没到手，
        ///   合成表就显示「(nullx3)」。那个 `"null"` 还是个**字符串**，
        ///   拼接不会崩、也不会报错，玩家只会以为是界面坏了（探针里全表都是 null）。
        ///   名字是道具的固有属性，跟玩家有没有它毫无关系 —— 所以应当问工厂，而不是问背包。
        ///
        /// 为什么先查背包：将来若允许改名/带词缀（"生锈的铁剑"），实例上的名字优先。
        /// </summary>
        public string GetItemNameById(int id)
        {
            foreach (Item item in Inventory)
            {
                if (item.Id == id)
                {
                    return item.Name;
                }
            }
            foreach ((int Id, string Name, Func<int, Item> Create) entry in ItemFactory.All)
            {
                if (entry.Id == id)
                {
                    return entry.Name;
                }
            }
            return "未知道具(" + id + ")";
        }
    }
}
