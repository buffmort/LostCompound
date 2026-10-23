using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;

namespace LostCompound.Items.Containers
{
    /// <summary>金属罐（配方 1）：容器。文档未给特殊效果。</summary>
    public sealed class MetalCan(int count) : Item(ItemIds.MetalCan, "金属罐", 10, true, count)
    {
        public override ItemType? Type => ItemType.MetalCan;
    }
}
