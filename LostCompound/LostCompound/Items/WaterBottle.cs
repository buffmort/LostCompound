namespace LostCompound.Items
{
    /// <summary>
    /// 水瓶：回满当前地图的水量。
    /// 注意：水量系统（熔岩瀑布 / 水淹洞穴）尚未实现，所以效果暂时是空的，
    /// 现在只作为「可获取的道具」存在，等水量系统做出来后在这里补效果。
    /// </summary>
    public sealed class WaterBottle(int count) : Item(ItemIds.WaterBottle, "水瓶", 10, true, count)
    {
        public override ItemType? Type => ItemType.WaterBottle;
    }
}
