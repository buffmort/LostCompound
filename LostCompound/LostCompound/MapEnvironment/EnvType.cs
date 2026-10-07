namespace LostCompound.MapEnvironment
{
    // 成员名改为 PascalCase；声明顺序与原来保持一致，从而各成员的整数值不变。
    public enum EnvType
    {
        None,
        LavaWaterfall,
        // 新增环境一律追加在末尾：EnvType 的整数值如果被存档/比较依赖，
        // 插在中间会改变已有成员的值。
        FloodedCavern,
    }
}
