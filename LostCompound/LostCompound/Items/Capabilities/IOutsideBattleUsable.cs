namespace LostCompound.Items.Capabilities
{
    /// <summary>
    /// 「这件道具**在战斗外也能用**」（地图上按 5 使用道具时，真的执行效果并消耗掉）。
    ///
    /// 为什么要这个接口：<see cref="IUseable"/> 默认走的是「战斗外只解释、不执行」那条路
    /// （见 `Player.ExplainItemsOutsideBattle`）—— 因为大部分道具是 AOE / 需要目标 / 需要水源的，
    /// 在没有敌人的房间里硬执行会很荒谬（毒气弹白白消耗、水瓶在没有水源的地方"回满水量"）。
    ///
    /// 但像**镜子(45)** 这种一次性消耗品，效果只看玩家自己（对换属性 + 轮换背包数量），
    /// 跟场上有没有怪毫无关系，就没有理由必须进战斗才能用。
    /// 于是用它标记一下：地图上选中时直接 `Use`，用完照常收尸。
    ///
    /// 与项目里其它能力位（`IOnDamaged` / `IDotAbsorber` / `ICannotBeTargeted`）同一思路：
    /// 调用方只问「这件东西能不能在战斗外用」，不认识具体道具类。
    /// </summary>
    public interface IOutsideBattleUsable
    {
    }
}
