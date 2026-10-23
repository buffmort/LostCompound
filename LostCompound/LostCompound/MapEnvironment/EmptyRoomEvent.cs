using LostCompound.Items;

namespace LostCompound.MapEnvironment
{
    /// <summary>
    /// 空房间事件的结果：一句播报 + 要发给玩家的道具。
    ///
    /// 为什么要有这么个小类型（而不是让环境直接 Console.WriteLine + 改 Player）：
    ///   环境负责**决策**（发生了什么、环境自己的状态怎么变），房间负责**执行**
    ///   （把话写进日志、把道具塞进背包）。这样环境永远不接触 Player，
    ///   既不违反 <see cref="Environment"/> 里「环境只提供修正量」的约定，
    ///   也让空房间的日志顺序集中在一个地方，不会出现「环境先播报、房间后播报」的错位。
    /// </summary>
    public sealed class EmptyRoomEvent
    {
        /// <summary>播报内容（写进日志）。空串表示这件事不需要播报。</summary>
        public string Message { get; init; } = "";

        /// <summary>本事件发给玩家的道具。可以为空（例如「什么也没找到」）。</summary>
        public List<Item> Items { get; init; } = [];
    }
}
