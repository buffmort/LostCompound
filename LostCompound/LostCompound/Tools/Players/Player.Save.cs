using LostCompound.Buffs;
using LostCompound.Items;

namespace LostCompound.Tools.Players
{
    /// <summary>
    /// 读档入口：把一份存档快照灌回 Player。
    ///
    /// 为什么单独一个 partial 文件而不是那些字段所在的地方：
    ///   这里的方法**只有存档代码会调**，而字段本身就散在 8 个 partial 里
    ///   （Hp/BaseXxx 在 Player.cs、背包在 Player.Inventory.cs、buff 在 Player.Buffs.cs…）。
    ///   全部集中在 `Player.Save.cs`，好处是「读档会碰哪些状态」一眼可见，
    ///   而不用在 8 个文件里找 internal 方法。
    ///
    /// ⚠️ 为什么这些字段**不**改成 `public set`：
    ///   那样就等于废掉「派生值只能通过 GrowBaseXxx 改」「背包只能通过 AddItem 进」这些纪律。
    ///   读档是**唯一的例外通道**，用 `internal` 把它关在程序集内部，
    ///   比为了存档方便把整套封装拆掉要划算得多。
    /// </summary>
    public partial class Player
    {
        /// <summary>
        /// 读档：恢复所有**存档里有**的状态。
        ///
        /// 刻意**不接收** Str / Atk / Def：它们是派生值，调用方在 <see cref="RecomputeStats"/>
        /// 之前/之后都不需要传 —— 恢复完基础值再重算一次，自然就对了。
        ///
        /// 刻意**不走** AddItem / AddBuff：
        ///   那两条通路会做「堆叠合并 / 容量检查 / 上限夹取 / 打印获得播报」，
        ///   而读档要的是「原样放回去」。用 AddItem 会让存档里的 `水瓶 x2` 与背包里的
        ///   `水瓶 x3` 合并成 5，还会在玩家一进游戏时刷一屏「你获得了…」。
        /// </summary>
        internal void RestoreFromSave(
            string name,
            double hp,
            double shield,
            double baseHp,
            double baseStr,
            double baseAtk,
            double baseDef,
            int inventoryCapacity,
            int accessorySlots,
            int fightCount,
            int skipTurn,
            bool isDead,
            IEnumerable<Item> inventory,
            IEnumerable<Item> accessories,
            IEnumerable<Buff> buffs)
        {
            Name = name;

            // 资源：原样恢复（Hp / Shield 不参与重算，见 CONVENTIONS 2.3）
            Hp = hp;
            Shield = shield;

            // 基础值：重算的起点
            BaseHp = baseHp;
            BaseStr = baseStr;
            BaseAtk = baseAtk;
            BaseDef = baseDef;

            // 成长进度
            InventoryCapacity = inventoryCapacity;
            AccessorySlots = accessorySlots;
            FightCount = fightCount;
            SkipTurn = skipTurn;
            IsDead = isDead;

            // 容器：先清空再整批塞回，不经过任何「合并 / 容量 / 播报」逻辑
            Inventory.Clear();
            Inventory.AddRange(inventory);

            Accessories.Clear();
            Accessories.AddRange(accessories);

            Buffs.Clear();
            Buffs.AddRange(buffs);
        }
    }
}
