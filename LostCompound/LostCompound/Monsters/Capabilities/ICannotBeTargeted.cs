namespace LostCompound.Monsters.Capabilities
{
    /// <summary>
    /// 「这个怪物**不能被玩家单体攻击选中**」。
    ///
    /// 目前只有食人鱼（DESIGN §6：无法被攻击，可被 AOE 打）。
    ///
    /// 为什么做成标记接口而不是让 Player.Attack 写 `m is Piranha`：
    ///   与道具的能力位同一套做法 —— 攻击流程只问「这个目标能不能被选」，
    ///   不认识具体怪物类；将来再加一只「潜行/隐身」的怪，攻击流程一行都不用改。
    ///
    /// ⚠️ 它只影响**单体选择**（<see cref="Tools.Players.Player.Attack"/>）；
    ///   全体 AOE（毒气弹 / 红磷 / 白磷 / 压缩沙瓶…）**照打不误** ——
    ///   那些道具直接遍历怪物列表，不经过选择步骤（Hyberty已确认这个口径）。
    /// </summary>
    public interface ICannotBeTargeted
    {
    }
}
