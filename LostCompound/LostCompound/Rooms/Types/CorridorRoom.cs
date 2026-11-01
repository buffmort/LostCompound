using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Rooms;
using LostCompound.Tools.Players;

namespace LostCompound.Rooms.Types
{
    /// <summary>
    /// **走廊**（不是房间）—— 房间与房间之间那格过道。
    ///
    /// 与 <see cref="BlankRoom"/> 的区别：
    ///   · Blank = 「这格没有房间」，会被画成「 . 」，用于「能不能在这儿造房间」的判断；
    ///   · 走廊 = 「有门、但不是房间」，**不画在地图上**（以撒的地图也只显示房间）。
    ///
    /// 为什么需要它：以撒式生成里房间不挨着，中间隔一格走廊。
    /// 如果走廊也用 BlankRoom，那它会被画成空盒子 / 「 . 」，整张图糊成网格，看不清房间怎么连；
    /// 如果当普通房间，又会多出「??」这种假房间。
    ///
    /// <see cref="Room.Type"/> 返回 <c>null</c> 是本类存在的核心 ——
    /// 地图渲染与「是不是房间」的判断都看 Type，null 就自然被排除在外。
    ///
    /// ⚠️ 玩家理论上走不进走廊格（门只连房间与房间，移动是按「门 → 相邻格」的），
    ///   但万一走进来（将来的传送之类），这里也不该崩：OnEnter 只打一句提示。
    /// </summary>
    public sealed class CorridorRoom : Room
    {
        public CorridorRoom(Environment environment) : base(null, environment)
        {
        }

        public override void OnEnter(Player p)
        {
            Console.WriteLine("一条窄窄的过道。");
        }

        public override void OnLeave(Player p)
        {
        }
    }
}
