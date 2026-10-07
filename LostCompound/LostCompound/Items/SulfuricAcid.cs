namespace LostCompound.Items
{
    /// <summary>
    /// 硫酸：用于在水淹洞穴 / 放射性区域开采金属矿与放射矿。
    /// 开采系统尚未实现，所以效果暂时为空，先作为可获取的材料存在。
    /// </summary>
    public sealed class SulfuricAcid(int count) : Item(ItemIds.SulfuricAcid, "硫酸", 10, true, count)
    {
        public override ItemType? Type => ItemType.SulfuricAcid;
    }
}
