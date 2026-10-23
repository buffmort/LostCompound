using LostCompound.Buffs.Types;

namespace LostCompound.Buffs
{
    /// <summary>
    /// Buff 工厂：ID → 名称 → 实例。与 Items\ItemFactory 同一套做法。
    ///
    /// 为什么需要它：buff 和道具一样是「一个类一个构造器」散着的，
    /// 没有任何地方能「按 ID 挂一个 buff」。调试发放（debug 130）、
    /// 将来的存档读档、事件按 ID 施加状态，都需要这张表。
    ///
    /// 维护约定：新增 buff 时**必须**在这里补一行，否则它无法被按 ID 创建。
    /// 顺序固定为 **ID 升序**（调试菜单直接按这个顺序打印）。
    ///
    /// 参数的语义：(stacks, turns) —— 两个数各代表什么由各 buff 自己定义
    /// （流血是「每回合伤害 / 剩余回合」，超雄是「强度 / 剩余回合」），
    /// 见 Buff 基类的注释。
    /// </summary>
    public static class BuffFactory
    {
        /// <summary>
        /// 所有可创建的 buff：ID、显示名、按 (效果量, 回合数) 造实例的工厂。
        ///
        /// 名称来源：各 buff 类的 <c>Name</c> 属性返回值（逐字抄，不重写）——
        /// 刻意不写死在这里的第二份文案，否则改名字要改两处、且两边会静默不一致。
        /// 这张表以 <see cref="BuffIds"/> 的常量为准 —— 每个常量都必须在这里有且仅有一行。
        /// </summary>
        public static IReadOnlyList<(int Id, string Name, Func<int, int, Buff> Create)> All { get; } =
        [
            (BuffIds.Bleed, "流血", (stacks, turns) => new BleedBuff(stacks, turns)),
            (BuffIds.SuperMale, "超雄综合症", (stacks, turns) => new SuperMaleBuff(stacks, turns)),
            (BuffIds.Strength, "力量", (stacks, turns) => new Strength(stacks, turns)),
        ];

        /// <summary>
        /// 按 ID 造 buff；未登记的 ID 返回 <c>null</c>（不抛异常 —— 调用方大多是调试/事件路径，
        /// 为一个不存在的 ID 崩掉整个游戏不值得）。
        /// </summary>
        public static Buff Create(int id, int stacks, int turns)
        {
            foreach ((int Id, string Name, Func<int, int, Buff> Create) entry in All)
            {
                if (entry.Id == id)
                {
                    return entry.Create(stacks, turns);
                }
            }
            return null;
        }

        /// <summary>ID 是否已登记。</summary>
        public static bool IsKnown(int id)
        {
            foreach ((int Id, string Name, Func<int, int, Buff> Create) entry in All)
            {
                if (entry.Id == id)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
