namespace LostCompound.Monsters
{
    /// <summary>
    /// 怪物的档位 —— 设计文档 §6「特色怪物」表的**「类型」列**（普通 / 精英）与 §6.1 的 **Boss**。
    ///
    /// 为什么用 enum 而不是 <c>bool IsBoss</c>（用户裁定：用 enum）：
    ///   1. 文档本来就是**三档**，不是两档 —— §6 的类型列写着「普通 / 精英」，§6.1 另起一节列 Boss。
    ///      <c>bool</c> 只能表达两档；将来要补「精英」就得再加一个 bool，
    ///      而两个 bool 会出现「IsElite 与 IsBoss 同时为 true」这种表达不出来、也没人校验的组合。
    ///   2. 判定方要的经常是「**不是** Boss」而不是「是 Boss」：毒气弹(26) 的效果就是
    ///      「10% 对非 boss 敌人造成 int.MAX_VALUE 伤害」，代码里写成
    ///      <c>mon.Rank != MonsterRank.Boss</c>；将来「只对精英生效」的效果也是同一形状。
    ///      enum 天然支持「按档位筛选」。
    ///   3. 档位与「哪种怪」（<see cref="MonsterType"/>）**正交**：MonsterType 回答「这是龙还是僵尸」，
    ///      本枚举回答「它属于哪一档」。同一种怪将来完全可能出现不同档位的变体
    ///      （「强化版僵尸」之类），所以不把档位塞进 MonsterType。
    ///
    /// 为什么 <see cref="Monster.Rank"/> 声明成 abstract 而不是「默认 Normal 的 virtual」：
    ///   漏声明的后果**不对称** —— 新怪忘了写档位时，默认 Normal 会让「毒气弹 10% 秒杀非 Boss」
    ///   对一只 Boss 生效（不报错、不崩溃，只是 Boss 被一发带走，事后极难查）；默认 Boss 又会让
    ///   普通怪全部免疫秒杀。做成 abstract 后**漏声明直接是编译错误**，在编译期拦住
    ///   （本项目一贯的口径：宁可在编译期炸，也不要静默算错，见 CONVENTIONS 2.3 的「静默全错」）。
    ///   代价只是每个新怪物多写一行 —— 而那一行正好就是文档 §6「类型」列的内容。
    /// </summary>
    public enum MonsterRank
    {
        /// <summary>普通怪（文档 §6 的「类型：普通」）。</summary>
        Normal,

        /// <summary>精英怪（文档 §6 的「类型：精英」）。</summary>
        Elite,

        /// <summary>Boss（文档 §6.1）。</summary>
        Boss,
    }
}
