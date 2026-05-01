using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 分支子条件类型
    /// </summary>
    public enum BranchSubConditionType
    {
        /// <summary>目标是否为特定种族/颜色</summary>
        TargetIsRaceOrColor,
        /// <summary>目标属性值大于/小于</summary>
        TargetAttributeCompare,
    }

    /// <summary>
    /// 分支子条件选项
    /// </summary>
    [Serializable]
    public class BranchSubCondition
    {
        public string Id;
        public BranchSubConditionType Type;
        public string DisplayName;
        public string Description;
        public string ParameterHint;
    }

    /// <summary>
    /// 分支条件定义
    /// </summary>
    [Serializable]
    public class BranchCondition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public List<BranchSubCondition> SubConditions = new List<BranchSubCondition>();
    }

    /// <summary>
    /// 分支配置（每个原子效果类型可定义多个分支场景）
    /// </summary>
    [Serializable]
    public class BranchConfig
    {
        public string Id;
        public string EffectTypeName;
        public string DisplayName;
        public List<BranchCondition> Conditions = new List<BranchCondition>();
        public int MaxChainedEffects = 2;
        public bool AllowNesting = false;
    }
}
