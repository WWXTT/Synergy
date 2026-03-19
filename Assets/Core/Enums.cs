namespace CardCore
{
    /// <summary>
    /// 效果类别
    /// </summary>
    public enum EffectSpeed
    {
        强制诱发,   // 条件达成自动扣除代价 触发
        可选诱发,    // 条件达成 玩家获得交互窗口 可选触发
        自由时点        // 当且仅当发动速度大于当前记速器速度时 可选诱发
    }

    /// <summary>
    /// 伤害类型
    /// </summary>
    public enum DamageType
    {
        /// <summary>战斗伤害</summary>
        Combat,
        /// <summary>非战斗伤害（效果伤害）</summary>
        NonCombat,
        /// <summary>无法防止的伤害</summary>
        CannotBePrevented,
        /// <summary>生命流失（不算作伤害）</summary>
        LifeLoss
    }
}