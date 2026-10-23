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

        /// <summary>
        /// 饰品栏（设计文档 8.2）：与背包分开存放，不占背包格。
        /// 初始 5 格，每通关 +1，上限 15。
        ///
        /// 按用户决定（方案 B）：任何「被动物件」（武器/防具/饰品）都能放进这里 ——
        /// 饰品栏本质上是「额外 5 个放被动物件的格子」，不限类别。
        /// </summary>
        public List<Item> Accessories { get; } = [];

        /// <summary>饰品栏容量。初始 5，每通关 +1，上限 15。</summary>
        public int AccessorySlots { get; private set; } = 5;

        /// <summary>饰品栏容量上限（设计文档 8.2）。</summary>
        public const int MaxAccessorySlots = 15;

        /// <summary>
        /// 通关一次后扩充饰品栏（+1 格，不超过上限）。
        /// 通关系统尚未实现，这个方法先留着，等进度系统做出来调用。
        /// </summary>
        public void GrowAccessorySlots()
        {
            if (AccessorySlots < MaxAccessorySlots)
            {
                AccessorySlots++;
                Console.WriteLine($"饰品栏扩充至 {AccessorySlots} 格！");
            }
        }

        /// <summary>
        /// 遍历玩家身上所有「生效中的被动物件」：背包 + 饰品栏
        /// （设计文档 7 节：放在背包或饰品栏里即生效）。
        ///
        /// 为什么抽成一个方法：派生属性加成（IEquippable.ApplyPassive）与护盾加成
        /// （IShieldSource.ApplyShield）**两条通道必须扫同一批道具** —— 各写一遍遍历，
        /// 早晚会漏掉一处（例如忘了饰品栏，那时「放进饰品栏的盔甲不加 DEF」不报错、
        /// 不崩溃，只是安静地少一点属性）。
        /// </summary>
        private void ForEachEquippedItem(Action<Item> action)
        {
            foreach (Item item in Inventory) action(item);
            foreach (Item item in Accessories) action(item);
        }

        // ==================== 饰品栏（设计文档 8.2） ====================

        /// <summary>
        /// 饰品栏交互界面：可以装备（把背包里的被动物件放上去）或卸下。
        /// 选 0 退出，不消耗回合。
        /// </summary>
        private void OpenAccessoryMenu()
        {
            while (true)
            {
                Console.WriteLine("===== 饰品栏 =====");
                ShowAccessories();

                Console.WriteLine("1.装备   2.卸下   0.返回");
                int action = ReadInt("饰品栏：1装备 2卸下 0返回");
                if (action == 0) return;
                // 输入流结束：菜单再问也读不到输入（ReadInt 会一直立刻返回 -1），
                // 直接退出饰品栏菜单，等价于玩家选了「0.返回」。手输 -1 时 _inputClosed 为 false，
                // 仍按原来的「什么都不做、重画菜单」处理，交互不变。
                if (_inputClosed) return;

                if (action == 1)
                {
                    // 只列出背包里真正能装备的（实现了 IEquippable 的被动物件）
                    List<int> equipable = [];
                    for (int i = 0; i < Inventory.Count; i++)
                    {
                        if (Inventory[i] is IEquippable)
                        {
                            equipable.Add(i);
                        }
                    }

                    if (equipable.Count == 0)
                    {
                        Console.WriteLine("背包里没有可以装备的道具。");
                        continue;
                    }

                    Console.WriteLine("可装备的道具（武器/防具/饰品等被动物件）：");
                    foreach (int idx in equipable)
                    {
                        string desc = Inventory[idx] is IEquippable eq ? "  " + eq.DescribePassive() : "";
                        Console.WriteLine("  [" + idx + "] " + Inventory[idx] + desc);
                    }

                    int pick = ReadInt("选择要装备的道具编号(-1取消)");
                    if (pick == -1)
                    {
                        // 输入流结束：不再重画菜单空转，直接退出饰品栏
                        if (_inputClosed) return;
                        continue;
                    }
                    EquipAccessory(pick);
                }
                else if (action == 2)
                {
                    if (Accessories.Count == 0)
                    {
                        Console.WriteLine("饰品栏是空的。");
                        continue;
                    }

                    int pick = ReadInt("选择要卸下的道具编号(-1取消)");
                    if (pick == -1)
                    {
                        // 输入流结束：不再重画菜单空转，直接退出饰品栏
                        if (_inputClosed) return;
                        continue;
                    }
                    UnequipAccessory(pick);
                }
            }
        }

        /// <summary>显示饰品栏内容。</summary>
        public void ShowAccessories()
        {
            Console.WriteLine($"===== 饰品栏 ({Accessories.Count}/{AccessorySlots}) =====");
            if (Accessories.Count == 0)
            {
                Console.WriteLine("（空）");
                return;
            }

            for (int i = 0; i < Accessories.Count; i++)
            {
                string desc = Accessories[i] is IEquippable eq ? "  " + eq.DescribePassive() : "";
                Console.WriteLine(i + "." + Accessories[i] + desc);
            }
        }

        /// <summary>
        /// 把背包里的第 index 个道具装备到饰品栏。
        ///
        /// 按你的决定（方案 B）：任何「被动物件」都能装 —— 武器、防具、饰品都算。
        /// 判断依据就是实现了 <see cref="IEquippable"/>（放背包/饰品栏都生效）。
        /// 材料、消耗品这类没有被动效果的，仍然装不上去。
        /// </summary>
        public bool EquipAccessory(int index)
        {
            if (index < 0 || index >= Inventory.Count)
            {
                Console.WriteLine("没有这个道具！");
                return false;
            }

            Item item = Inventory[index];
            if (item is not IEquippable)
            {
                Console.WriteLine(item.Name + " 没有被动效果，无法装备到饰品栏。");
                return false;
            }

            if (Accessories.Count >= AccessorySlots)
            {
                Console.WriteLine($"饰品栏已满（{AccessorySlots} 格）！");
                return false;
            }

            Inventory.RemoveAt(index);
            Accessories.Add(item);
            Console.WriteLine("你把 " + item.Name + " 装备到了饰品栏。");

            // 被动物件会加属性，装/卸之后要重算，否则属性不会立刻变化
            ApplyEquipmentPassives();
            RefreshStatus();
            return true;
        }

        /// <summary>
        /// 把饰品栏里的第 index 个道具卸回背包。
        ///
        /// ⚠️ 背包满时**拒绝**卸下，而不是照旧塞进去或静默丢弃（本次修掉的一个真 bug）：
        ///   原先这里直接 <c>Inventory.Add(item)</c>，完全不看容量 ——
        ///   背包 15/15 时卸一个饰品就变成 16 格，容量上限形同虚设
        ///   （<see cref="AddItem"/> 的容量检查只保护了「进包」的另一条路径）。
        ///   为什么选「拒绝」而不是走 AddItem（满了就丢）：
        ///     走 AddItem 的话，玩家手上这件装备会在「卸下」这个动作里被**静默销毁** ——
        ///     卸装备的本意是把它收好，丢掉装备是玩家绝不会预期的结果，
        ///     而且丢失无声无息（只有一行「被丢弃了」混在滚动日志里），比多占一格严重得多。
        ///   为什么也不选「不让卸」的另一种写法（例如卸下时把背包整屏锁住）：
        ///     那样玩家在战斗中等于被永久锁死饰品栏。提示一句、让玩家先腾一格最直观。
        /// </summary>
        public bool UnequipAccessory(int index)
        {
            if (index < 0 || index >= Accessories.Count)
            {
                Console.WriteLine("饰品栏没有这个位置！");
                return false;
            }

            // 容量检查必须在「从 Accessories 移除」之前：否则检查失败时物品已经被摘下来了，
            // 它既不在饰品栏也不在背包里 —— 直接消失。
            if (Inventory.Count >= InventoryCapacity)
            {
                Console.WriteLine($"背包已满（{InventoryCapacity} 格），先腾出一格再卸下 {Accessories[index].Name}。");
                return false;
            }

            Item item = Accessories[index];
            Accessories.RemoveAt(index);
            Inventory.Add(item);
            Console.WriteLine("你把 " + item.Name + " 放回了背包。");

            ApplyEquipmentPassives();
            RefreshStatus();
            return true;
        }

        /// <summary>
        /// 收走「身上已经用坏」的被动道具 —— **背包 + 饰品栏都扫**。
        /// 两条装备通道在用坏自己后会走到这里：
        ///   · <see cref="IOnDamaged"/> 的受伤通知（斗篷记满 50 次即破损，见 <see cref="NotifyDamaged"/>）；
        ///   · <see cref="IDotAbsorber"/> 的吸收结算（<see cref="TakeDotDamage"/>；目前没有实现者）。
        /// 判定仍旧只走 IChangeable.ShouldRemove()，谁都不在这里另立规则。
        ///
        /// 为什么需要这条独立通路：既有的 IChangeable 收尸（<see cref="HandleChangeableItems"/> /
        /// <see cref="RemoveConsumedItems"/>）**只遍历 Inventory**，饰品栏从来没有被扫过。
        /// 对堆叠消耗品无所谓（它们进不了饰品栏），但斗篷是「饰品」，会待在饰品栏里 ——
        /// 不扫饰品栏就会留下一个「已经破损、却还挂着 +5 DEF 与 20% 免伤」的幽灵道具，
        /// 不报错、不崩溃，只是数值静默不符。
        ///
        /// 收完要 RecomputeStats()：装备少了一件，派生值必须立刻跟上
        /// （这里**不**调 ApplyEquipmentPassives —— 那个会顺手把护盾清零，
        ///   而「护盾清零」只在换装/开战时才是设计意图（CONVENTIONS 三），
        ///   不该被一次受伤结算触发）。
        /// </summary>
        private void RemoveBrokenEquippedItems()
        {
            List<Item> toAdd = [];
            bool removedFromInventory = SweepConsumed(Inventory, toAdd);
            bool removedFromAccessories = SweepConsumed(Accessories, toAdd);

            foreach (Item newItem in toAdd)
            {
                AddItem(newItem);
            }

            if (removedFromInventory || removedFromAccessories) RecomputeStats();
        }

        /// <summary>
        /// 让「当前装备」的被动效果在玩家身上重新生效一次（开战 / 换装 / 卸装时调用）。
        ///
        /// 关键：先重算派生值（从基础值来），再加装备加成。
        /// 如果直接 Atk += 加成，打第二场战斗时会再叠一次，10 场之后 ATK 就爆了。
        /// 所以第一步就是 RecomputeStats 里的「先还原成基础值」。
        /// 顺序也不能反：RecomputeStats 里的装备加成是 `+=`，必须在赋值之后（见其注释）。
        ///
        /// 第二步把护盾也重来一遍（清零 + 装备的护盾加成）：
        ///   ⚠️ 「换装备 / 开战时护盾清零」是**刻意的游戏设计**（CONVENTIONS 三），
        ///   不是重算派生值的副作用 —— 换装等同重新整理防御姿态，战斗外攒的盾随之失效。
        ///   这条不能挪走。
        /// </summary>
        private void ApplyEquipmentPassives()
        {
            RecomputeStats();
            ResetShieldForTurn();
        }
    }
}
