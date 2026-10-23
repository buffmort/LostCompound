using LostCompound.MapEnvironment.Environments;

namespace LostCompound.MapEnvironment
{
    /// <summary>按 <see cref="EnvType"/> 创建环境对象。</summary>
    public static class EnvironmentFactory
    {
        /// <summary>
        /// 创建环境对象。若枚举是 None、或对应环境尚未实现，返回 <c>null</c>，
        /// 调用方（房间）需要处理「没有环境」的情况。
        /// </summary>
        public static Environment Create(EnvType type) => type switch
        {
            EnvType.None => null,
            EnvType.LavaWaterfall => new LavaWaterfall(),
            EnvType.FloodedCavern => new FloodedCavern(),
            // 其余环境（沙漠 / 矿洞 / 墓地 / 放射性区域 / 雾都城市）尚未实现，先返回 null
            _ => null,
        };
    }
}
