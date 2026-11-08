namespace LostCompound.Monsters
{
    /// <summary>
    /// 伤害的**元素归属** —— 用来表达「这一击是磷系 / 火系」这类「按来源触发特殊反应」的规则。
    ///
    /// 为什么需要它：木乃伊（<see cref="Normal.Mummy"/>）的机制是「**被磷家族道具打中时**
    /// 才会中磷灼烧」。而伤害入口只有一个 <c>TakeDamage(double)</c>，怪物没法从数字上
    /// 看出这一击是什么打的 —— 不给一个「元素」标记，这条规则就写不出来。
    ///
    /// 为什么不用「道具类型判断」：判定方（怪物）不该认识白磷 / 红磷 / 磷化剑这几个具体类。
    /// 道具自己声明「我这一击属于哪一族」，怪物只回答「这种元素我会不会中招」——
    /// 以后再加火系道具，只要标成 <see cref="Fire"/>，木乃伊那边一行都不用改。
    ///
    /// ⚠️ 新增一个元素时：在这里加成员，并让对应的道具在造成伤害后调
    /// <see cref="Monster.OnElementalHit"/>。
    /// </summary>
    public enum DamageElement
    {
        /// <summary>没有元素归属（普通攻击、环境伤害等）。</summary>
        None = 0,

        /// <summary>磷系：白磷 / 红磷 / 磷化剑。</summary>
        Phosphorus = 1,

        /// <summary>火系（暂时还没有道具用，留给「之后的火系道具也算」这条裁定）。</summary>
        Fire = 2,
    }
}
