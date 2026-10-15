namespace LostCompound.Items
{
    /// <summary>
    /// 布料（ID 41）：材料，堆叠 99。没有主动效果，纯粹是合成材料。
    /// 获取：5 木头合成（配方 0，见 <see cref="Tools.Player.AddDefaultRecipes"/>）。
    ///
    /// 当前唯一的下游用途是斗篷（ID 27，3 木头 + 1 金属 + 1 布料）。
    ///
    /// ⚠️ 设计文档核对结果（已复核）：<c>DESIGN.md</c> 第九章配方 0 表**已有** ID 41 这一行
    ///   ——「41 布料 材料 99 5 木头合成 原材料」，与本类一致。
    ///   （本注释此前写着「文档没有 41 这一行、文档待补」，那是文档补上之前的旧状态，已过期。）
    ///   ID 41 落在文档分段的 30-40 之外，这里照「允许跳号、不挤号」的约定直接用 41，不去抢已有的号。
    /// </summary>
    public sealed class Cloth(int count) : Item(ItemIds.Cloth, "布料", 99, true, count)
    {
        public override ItemType? Type => ItemType.Cloth;
    }
}
