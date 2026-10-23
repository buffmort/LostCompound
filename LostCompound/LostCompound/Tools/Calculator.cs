namespace LostCompound.Tools
{
    public static class Calculator
    {
        public static double CalculateAttack(double str, double atk) => 2 + str * Math.Sqrt(atk);

        /// <summary>
        /// 普通模式的 DEF 减免系数（设计文档 7.2：RealDmg = Basic - 0.5（困难 1）× def）。
        ///
        /// ⚠️ 困难模式的 1 还没实现（缺难度系统）—— 等难度系统做出来后，这个常量要变成
        ///    按难度取值（普通 0.5 / 困难 1），届时本文档里所有调用点都不用改。
        /// </summary>
        public const double DefCoefficient = 0.5;

        /// <summary>
        /// 伤害过 DEF 减免：<c>RealDmg = max(dmg - DefCoefficient × def, 1)</c>。
        ///
        /// 「最低取 1」是设计文档 7.2 的硬规则，与系数无关 —— 换成 0.5 之后这一条**仍然生效**，
        /// 只是更难被夹到（0.5 × def 的减免量本来就小）。Hyberty 的例外（到最低时反造成
        /// 玩家最大生命值 5%）属于 Hyberty 战自己的规则，不在本方法里。
        /// </summary>
        public static double ApplyDefense(double def, double dmg) => Math.Max(dmg - DefCoefficient * def, 1);
    }
}
