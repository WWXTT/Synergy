using System;
using System.Collections.Generic;
using cfg;
using CardCore.Data;

namespace CardCore
{
    /// <summary>
    /// 效果筛选数据
    /// 用于效果列表的筛选条件
    /// </summary>
    [Serializable]
    public class EffectFilterData
    {
        /// <summary>选中的元素类型列表（多选）</summary>
        public List<ManaType> SelectedManaTypes = new List<ManaType>();

        /// <summary>选中的功能分类列表（多选）</summary>
        public List<EffectCategory> SelectedCategories = new List<EffectCategory>();

        /// <summary>选中的发动速度列表（多选）</summary>
        public List<EffectSpeed> SelectedSpeeds = new List<EffectSpeed>();

        /// <summary>已解锁的元素类型（用于UI显示控制）</summary>
        public List<ManaType> UnlockedManaTypes = new List<ManaType>();

        /// <summary>搜索关键词</summary>
        public string SearchKeyword = string.Empty;

        /// <summary>是否显示自定义效果</summary>
        public bool ShowCustomEffects = true;

        /// <summary>是否显示预制效果</summary>
        public bool ShowPresetEffects = true;

        /// <summary>
        /// 检查筛选条件是否为空
        /// </summary>
        public bool IsEmpty()
        {
            return (SelectedManaTypes == null || SelectedManaTypes.Count == 0) &&
                   (SelectedCategories == null || SelectedCategories.Count == 0) &&
                   (SelectedSpeeds == null || SelectedSpeeds.Count == 0) &&
                   string.IsNullOrEmpty(SearchKeyword);
        }

        /// <summary>
        /// 重置筛选条件
        /// </summary>
        public void Reset()
        {
            SelectedManaTypes?.Clear();
            SelectedCategories?.Clear();
            SelectedSpeeds?.Clear();
            SearchKeyword = string.Empty;
            ShowCustomEffects = true;
            ShowPresetEffects = true;
        }

        /// <summary>
        /// 复制筛选数据
        /// </summary>
        public EffectFilterData Clone()
        {
            return new EffectFilterData
            {
                SelectedManaTypes = new List<ManaType>(SelectedManaTypes ?? new List<ManaType>()),
                SelectedCategories = new List<EffectCategory>(SelectedCategories ?? new List<EffectCategory>()),
                SelectedSpeeds = new List<EffectSpeed>(SelectedSpeeds ?? new List<EffectSpeed>()),
                UnlockedManaTypes = new List<ManaType>(UnlockedManaTypes ?? new List<ManaType>()),
                SearchKeyword = SearchKeyword,
                ShowCustomEffects = ShowCustomEffects,
                ShowPresetEffects = ShowPresetEffects
            };
        }
    }
}
