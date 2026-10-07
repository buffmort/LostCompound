using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>一条合成配方的抽象：材料表 + 产物 + 能否合成/执行的判定。</summary>
    public interface ICraftable
    {
        /// <summary>配方名（展示用，如「热硫」）。</summary>
        string Name { get; }

        /// <summary>材料表：每项是 [道具 ID, 数量] 两个元素的数组。</summary>
        int[][] Materials { get; }

        /// <summary>合成产物。</summary>
        Item Product { get; }

        /// <summary>检查玩家的背包是否满足本配方的材料要求。</summary>
        bool CanCraft(Player player);

        /// <summary>执行合成：扣材料、发产物。</summary>
        void Craft(Player player);

        /// <summary>把材料表拼成给玩家看的文字（如「(木头x2 + 金属x2)」）。</summary>
        string DescribeMaterials(Player player);
    }
}
