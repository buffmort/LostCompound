using LostCompound.Tools;
using LostCompound.Items;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Capabilities
{
    /// <summary>一条合成配方的抽象：材料表 + 产物 + 能否合成/执行的判定。</summary>
    public interface ICraftable
    {
        /// <summary>配方名（展示用，如「热硫」）。</summary>
        string Name { get; }

        /// <summary>材料表：每项是 [道具 ID, 数量] 两个元素的数组。</summary>
        int[][] Materials { get; }

        /// <summary>
        /// 造一个**全新的**产物实例（每次合成都要造新的）。
        ///
        /// ⚠️ 为什么是「造一个」的方法，而不是「持有一个产物」的属性：
        ///   原先接口上写的是 <c>Item Product { get; }</c>，配方在构造时 <c>new</c> 一个产物实例、
        ///   一直持有它。而 <c>Player.AddItem(Item)</c> **不做任何复制** —— 它把传进来的那个对象
        ///   直接放进 <c>Inventory</c>（或并入同名堆叠）。于是配方持有的实例和背包里那一格
        ///   **就是同一个对象**，两种坏法都出现了：
        ///     ① 那个对象被当材料消耗掉 / 被用掉之后 <c>Count</c> 归零，
        ///        配方再合成时 <c>AddItem</c> 里的 <c>int remaining = newItem.Count;</c> 读到 0，
        ///        循环第一个判断就 break、<c>added</c> 保持 0 —— **材料照扣，产物没有**，
        ///        连「你获得了」都不打印（静默吞材料）；
        ///     ② 那个对象 <c>Count &gt; 0</c>（比如就放在背包里）时，<c>AddItem</c> 把它自己
        ///        当成「已有的同名堆叠」再叠一遍：Count 1 → 2 → 4 → 8（不是每次 +1），
        ///        也就是**合成会翻倍**。
        ///   改成工厂之后，配方手里从来没有产物实例，每次合成都是一个与背包毫无关系的新对象，
        ///   上述两条都不可能再发生。
        /// </summary>
        Item CreateProduct();

        /// <summary>检查玩家的背包是否满足本配方的材料要求。</summary>
        bool CanCraft(Player player);

        /// <summary>执行合成：扣材料、发产物。</summary>
        void Craft(Player player);

        /// <summary>把材料表拼成给玩家看的文字（如「(木头x2 + 金属x2)」）。</summary>
        string DescribeMaterials(Player player);
    }
}
