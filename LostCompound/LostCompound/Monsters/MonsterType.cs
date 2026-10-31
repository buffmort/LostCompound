namespace LostCompound.Monsters
{
    public enum MonsterType
    {
        Dragon,
        Dummy,
        LavaSlime,
        TwistedPair1,
        TwistedPair2,
        Zombie,
        Skeleton,
        ShieldSoldier,   // 只追加在末尾（enum 成员的值不落库，但仍按项目约定避免改动已有成员）

        // 水淹洞穴专属（DESIGN §6 地图 2）
        Piranha,
        DiverZombie,
    }
}
