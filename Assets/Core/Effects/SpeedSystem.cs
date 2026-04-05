using System;

namespace CardCore
{
    /// <summary>
    /// 速度等级常量
    /// 速度从0开始，可以通过支付代价提升
    /// </summary>
    public static class SpeedLevel
    {
        /// <summary>基础速度 - 最低速度</summary>
        public const int Base = 0;

        /// <summary>普通速度</summary>
        public const int Normal = 1;

        /// <summary>快速速度</summary>
        public const int Quick = 2;

        /// <summary>瞬时速度</summary>
        public const int Instant = 3;

        /// <summary>无上限 - 理论上速度可以通过支付代价无限提升</summary>
        public const int Unlimited = int.MaxValue;
    }

    /// <summary>
    /// 效果发动类型（决定入栈优先级）
    /// </summary>
    public enum EffectActivationType
    {
        /// <summary>强制发动 - 必须发动，最高优先级，满足条件自动入栈</summary>
        Mandatory,

        /// <summary>自动发动 - 满足条件自动发动，次优先级</summary>
        Automatic,

        /// <summary>自由发动 - 玩家选择是否发动，需要轮询</summary>
        Voluntary,
    }

    /// <summary>
    /// 全局速度计数器
    /// 场上唯一的速度计，用于控制连锁深度
    /// </summary>
    public class SpeedCounter
    {
        private int _currentSpeed = 0;
        private bool _isResolving = false;
        private int _peakSpeed = 0;  // 本轮连锁的最高速度（用于记录）

        /// <summary>当前速度值</summary>
        public int CurrentSpeed => _currentSpeed;

        /// <summary>是否正在结算中</summary>
        public bool IsResolving => _isResolving;

        /// <summary>本轮连锁的最高速度</summary>
        public int PeakSpeed => _peakSpeed;

        /// <summary>
        /// 提升速度计数器（效果发动时）
        /// </summary>
        /// <param name="effectSpeed">效果的发动速度</param>
        public void RaiseTo(int effectSpeed)
        {
            if (effectSpeed > _currentSpeed)
            {
                _currentSpeed = effectSpeed;
                if (effectSpeed > _peakSpeed)
                    _peakSpeed = effectSpeed;
            }
        }

        /// <summary>
        /// 检查效果是否可以发动
        /// </summary>
        /// <param name="effectSpeed">效果的发动速度</param>
        /// <param name="activationType">效果的发动类型</param>
        /// <returns>是否可以发动</returns>
        public bool CanActivate(int effectSpeed, EffectActivationType activationType)
        {
            // 速度必须大于当前计数器
            if (effectSpeed <= _currentSpeed)
                return false;

            // 结算中只允许强制/自动效果加入
            if (_isResolving && activationType == EffectActivationType.Voluntary)
                return false;

            return true;
        }

        /// <summary>
        /// 开始结算
        /// </summary>
        public void BeginResolution()
        {
            _isResolving = true;
        }

        /// <summary>
        /// 结算完成一个效果，计数器-1
        /// </summary>
        /// <returns>新的速度值</returns>
        public int Decrement()
        {
            if (_currentSpeed > 0)
                _currentSpeed--;
            return _currentSpeed;
        }

        /// <summary>
        /// 重置计数器（连锁结算完成后）
        /// </summary>
        public void Reset()
        {
            _currentSpeed = 0;
            _isResolving = false;
            _peakSpeed = 0;
        }

        /// <summary>
        /// 获取状态描述
        /// </summary>
        public string GetStateDescription()
        {
            string state = _isResolving ? "结算中" : "等待中";
            return $"速度计数器: {_currentSpeed} ({state})";
        }
    }

    /// <summary>
    /// 速度修正器
    /// 用于临时修改效果速度
    /// </summary>
    [Serializable]
    public class SpeedModifier
    {
        /// <summary>固定加成</summary>
        public int FlatBonus = 0;

        /// <summary>是否忽略栈限制（特殊效果）</summary>
        public bool IgnoreStackLimit = false;

        /// <summary>
        /// 应用修正
        /// </summary>
        public int Apply(int baseSpeed)
        {
            return baseSpeed + FlatBonus;
        }
    }

    /// <summary>
    /// 速度提升支付
    /// 定义如何通过支付代价来提升速度
    /// </summary>
    [Serializable]
    public class SpeedBoostPayment
    {
        /// <summary>提升的速度值</summary>
        public int SpeedIncrease;

        /// <summary>最大提升次数（0 = 无限制）</summary>
        public int MaxUses = 0;
    }

    /// <summary>
    /// 速度计算器
    /// 用于计算效果的最终发动速度
    /// </summary>
    public static class SpeedCalculator
    {
        /// <summary>
        /// 计算效果的实际发动速度
        /// </summary>
        /// <param name="baseSpeed">效果基础速度</param>
        /// <param name="activator">发动者</param>
        /// <param name="activePlayer">主回合玩家</param>
        /// <param name="paidBoost">支付代价提升的速度</param>
        /// <returns>实际发动速度</returns>
        public static int CalculateActivationSpeed(
            int baseSpeed,
            Player activator,
            Player activePlayer,
            int paidBoost = 0)
        {
            int speed = baseSpeed;

            // 主回合玩家加成 +1
            if (activator == activePlayer)
                speed += 1;

            // 支付的代价提升
            speed += paidBoost;

            return speed;
        }

        /// <summary>
        /// 检查是否可以在当前速度下发动
        /// </summary>
        public static bool CanActivateAtSpeed(
            int effectSpeed,
            int currentSpeed,
            bool isResolving,
            EffectActivationType activationType)
        {
            // 速度必须大于当前计数器
            if (effectSpeed <= currentSpeed)
                return false;

            // 结算中只允许强制/自动效果
            if (isResolving && activationType == EffectActivationType.Voluntary)
                return false;

            return true;
        }
    }
}
