namespace HundredSchools
{
    /// <summary>
    /// 诸子百家学派枚举。
    /// 影响玩家的基础属性倾向、可选技能树和 Sprite 颜色。
    /// GDD v1.9：五学派体系（儒/法/道/墨/无）。
    /// </summary>
    public enum ESchool
    {
        /// <summary>儒家 —— 重礼乐教化，弹幕溅射（Splash）</summary>
        Confucian,

        /// <summary>法家 —— 严刑峻法，弹幕穿透（Pierce）</summary>
        Legalist,

        /// <summary>道家 —— 道法自然，弹幕回转（Return）</summary>
        Taoist,

        /// <summary>墨家 —— 兼爱非攻，普通弹幕（Normal）</summary>
        Mohist,

        /// <summary>无学派 —— 散兵游勇，普通弹幕（Normal）</summary>
        None
    }

    /// <summary>
    /// 武器 / 技艺流派枚举。
    /// 决定玩家的攻击方式和弹幕形态。
    /// </summary>
    public enum EWeapon
    {
        /// <summary>射艺 —— 远程弓矢 / 弹幕攻击</summary>
        Archery,

        /// <summary>御艺 —— 驾驭战车，冲撞近战</summary>
        Chariot,

        /// <summary>礼艺 —— 以礼乐化人，范围 AOE / Buff 光环</summary>
        Ritual
    }
}
