using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 碘（配方 1，ID 14）：原材料。文档里没有获取途径，所以目前只能靠调试或后续系统产出。
    /// </summary>
    public sealed class Iodine(int count) : Item(ItemIds.Iodine, "碘", 99, true, count)
    {
        public override ItemType? Type => ItemType.Iodine;
    }
}
