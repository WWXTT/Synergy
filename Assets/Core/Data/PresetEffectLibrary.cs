using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using cfg;
using CardCore.Data;

namespace CardCore
{
    /// <summary>
    /// 效果功能分类
    /// </summary>
    public enum EffectCategory
    {
        /// <summary>伤害类 - 造成各类伤害</summary>
        Damage,
        /// <summary>治疗类 - 回复生命</summary>
        Heal,
        /// <summary>抽卡类 - 抽牌相关</summary>
        Draw,
        /// <summary>控制类 - 控制对手/卡牌</summary>
        Control,
        /// <summary>破坏类 - 破坏卡牌</summary>
        Destroy,
        /// <summary>增益类 - 强化己方</summary>
        Buff,
        /// <summary>减益类 - 削弱敌方</summary>
        Debuff,
        /// <summary>移动类 - 卡牌区域移动</summary>
        Move,
        /// <summary>费用降低 - 添加负面效果降低费用</summary>
        CostReduction,
        /// <summary>特殊类 - 其他效果</summary>
        Special
    }

    /// <summary>
    /// 预制效果数据 - 扩展效果定义数据以包含分类信息
    /// </summary>
    [Serializable]
    public class PresetEffectData
    {
        /// <summary>效果定义数据</summary>
        public EffectDefinitionData EffectData;

        /// <summary>效果功能分类</summary>
        public int Category;

        /// <summary>是否为预制效果</summary>
        public bool IsPreset = true;

        /// <summary>来源标签颜色</summary>
        public string SourceColor = "green";

        /// <summary>
        /// 获取分类枚举
        /// </summary>
        public EffectCategory GetCategory()
        {
            return (EffectCategory)Category;
        }

        /// <summary>
        /// 获取分类显示名���
        /// </summary>
        public string GetCategoryDisplayName()
        {
            return GetCategory() switch
            {
                EffectCategory.Damage => "伤害",
                EffectCategory.Heal => "治疗",
                EffectCategory.Draw => "抽卡",
                EffectCategory.Control => "控制",
                EffectCategory.Destroy => "破坏",
                EffectCategory.Buff => "增益",
                EffectCategory.Debuff => "减益",
                EffectCategory.Move => "移动",
                EffectCategory.CostReduction => "降费",
                EffectCategory.Special => "特殊",
                _ => "其他"
            };
        }
    }

    /// <summary>
    /// 预制效果库
    /// 从StreamingAssets加载和管理预制效果
    /// </summary>
    public class PresetEffectLibrary
    {
        #region 单例

        private static PresetEffectLibrary _instance;
        public static PresetEffectLibrary Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PresetEffectLibrary();
                }
                return _instance;
            }
        }

        #endregion

        #region 常量

        /// <summary>预制效果保存目录</summary>
        private const string PRESET_DIRECTORY = "PresetEffects";

        /// <summary>文件扩展名</summary>
        private const string FILE_EXTENSION = ".json";

        #endregion

        #region 字段

        /// <summary>预制效果保存路径</summary>
        private readonly string _presetPath;

        /// <summary>缓存预制效果</summary>
        private readonly Dictionary<string, PresetEffectData> _cachedPresets = new Dictionary<string, PresetEffectData>();

        /// <summary>按分类索引</summary>
        private readonly Dictionary<EffectCategory, List<PresetEffectData>> _presetsByCategory = new Dictionary<EffectCategory, List<PresetEffectData>>();

        /// <summary>按元素类型索引</summary>
        private readonly Dictionary<ManaType, List<PresetEffectData>> _presetsByManaType = new Dictionary<ManaType, List<PresetEffectData>>();

        /// <summary>是否已加载</summary>
        public bool IsLoaded { get; private set; }

        #endregion

        #region 构造函数

        private PresetEffectLibrary()
        {
            _presetPath = Path.Combine(Application.streamingAssetsPath, PRESET_DIRECTORY);

            // 初始化分类字典
            foreach (EffectCategory category in Enum.GetValues(typeof(EffectCategory)))
            {
                _presetsByCategory[category] = new List<PresetEffectData>();
            }

            // 初始化元素类型字典
            foreach (ManaType manaType in Enum.GetValues(typeof(ManaType)))
            {
                _presetsByManaType[manaType] = new List<PresetEffectData>();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 从StreamingAssets加载预制效果
        /// </summary>
        public async UniTask<List<PresetEffectData>> LoadFromJSON()
        {
            var result = new List<PresetEffectData>();

            try
            {
                // 清空缓存
                _cachedPresets.Clear();
                foreach (var list in _presetsByCategory.Values)
                {
                    list.Clear();
                }
                foreach (var list in _presetsByManaType.Values)
                {
                    list.Clear();
                }

                // 检查目录是否存在
                if (!Directory.Exists(_presetPath))
                {
                    Directory.CreateDirectory(_presetPath);
                    Debug.LogWarning($"PresetEffectLibrary: 预制效果目录不存在，已创建: {_presetPath}");
                    IsLoaded = true;
                    return result;
                }

                // 获取所有JSON文件
                string[] files = Directory.GetFiles(_presetPath, $"*{FILE_EXTENSION}");

                foreach (string file in files)
                {
                    try
                    {
                        string json = await ReadTextAsync(file);

                        if (!string.IsNullOrEmpty(json))
                        {
                            var presetData = JsonUtility.FromJson<PresetEffectData>(json);

                            if (presetData?.EffectData != null && !string.IsNullOrEmpty(presetData.EffectData.Id))
                            {
                                result.Add(presetData);
                                CachePreset(presetData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"PresetEffectLibrary: 加载文件 {file} 失败 - {ex.Message}");
                    }
                }

                IsLoaded = true;
                Debug.Log($"PresetEffectLibrary: 加载了 {result.Count} 个预制效果");
            }
            catch (Exception ex)
            {
                Debug.LogError($"PresetEffectLibrary: 加载预制效果失败 - {ex.Message}");
                IsLoaded = true;
            }

            return result;
        }

        /// <summary>
        /// 按筛选条件获取效果
        /// </summary>
        /// <param name="manaTypes">元素类型列表（null表示不限）</param>
        /// <param name="categories">功能分类列表（null表示不限）</param>
        /// <param name="speeds">发动速度列表（null表示不限）</param>
        /// <returns>符合条件的效果列表</returns>
        public List<PresetEffectData> GetEffectsByFilter(
            List<ManaType> manaTypes = null,
            List<EffectCategory> categories = null,
            List<EffectSpeed> speeds = null)
        {
            IEnumerable<PresetEffectData> query = _cachedPresets.Values;

            // 按元素类型筛选
            if (manaTypes != null && manaTypes.Count > 0)
            {
                query = query.Where(p => manaTypes.Contains((ManaType)(p.EffectData.Cost?.ElementCosts?.FirstOrDefault()?.ManaType ?? (int)ManaType.灰色)));
            }

            // 按功能分类筛选
            if (categories != null && categories.Count > 0)
            {
                query = query.Where(p => categories.Contains(p.GetCategory()));
            }

            // 按发动速度筛选
            if (speeds != null && speeds.Count > 0)
            {
                query = query.Where(p => speeds.Contains((EffectSpeed)p.EffectData.ActivationType));
            }

            return query.ToList();
        }

        /// <summary>
        /// 按ID获取效果
        /// </summary>
        public PresetEffectData GetEffectById(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return null;

            _cachedPresets.TryGetValue(effectId, out var preset);
            return preset;
        }

        /// <summary>
        /// 获取所有效果功能分类
        /// </summary>
        public List<EffectCategory> GetEffectCategories()
        {
            return _presetsByCategory.Keys.Where(k => _presetsByCategory[k].Count > 0).ToList();
        }

        /// <summary>
        /// 获取所有预制效果
        /// </summary>
        public List<PresetEffectData> GetAllPresets()
        {
            return _cachedPresets.Values.ToList();
        }

        /// <summary>
        /// 保存预制效果到文件
        /// </summary>
        public async UniTask<bool> SavePresetAsync(PresetEffectData preset)
        {
            if (preset?.EffectData == null || string.IsNullOrEmpty(preset.EffectData.Id))
            {
                Debug.LogError("PresetEffectLibrary: 无效的预制效果数据");
                return false;
            }

            try
            {
                // 确保目录存在
                if (!Directory.Exists(_presetPath))
                {
                    Directory.CreateDirectory(_presetPath);
                }

                string filePath = GetPresetFilePath(preset.EffectData.Id);
                string json = JsonUtility.ToJson(preset, true);

                await WriteTextAsync(filePath, json);

                // 更新缓存
                CachePreset(preset);

                Debug.Log($"PresetEffectLibrary: 保存预制效果 {preset.EffectData.DisplayName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"PresetEffectLibrary: 保存预制效果失败 - {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 缓存预制效果
        /// </summary>
        private void CachePreset(PresetEffectData preset)
        {
            if (preset?.EffectData == null) return;

            _cachedPresets[preset.EffectData.Id] = preset;

            // 添加到分类索引
            var category = preset.GetCategory();
            if (_presetsByCategory.ContainsKey(category))
            {
                if (!_presetsByCategory[category].Contains(preset))
                {
                    _presetsByCategory[category].Add(preset);
                }
            }

            // 添加到元素类型索引
            var manaType = (ManaType)(preset.EffectData.Cost?.ElementCosts?.FirstOrDefault()?.ManaType ?? (int)ManaType.灰色);
            if (_presetsByManaType.ContainsKey(manaType))
            {
                if (!_presetsByManaType[manaType].Contains(preset))
                {
                    _presetsByManaType[manaType].Add(preset);
                }
            }
        }

        /// <summary>
        /// 获取预制效果文件路径
        /// </summary>
        private string GetPresetFilePath(string effectId)
        {
            string safeId = string.Join("_", effectId.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_presetPath, $"{safeId}{FILE_EXTENSION}");
        }

        /// <summary>
        /// 异步读取文本
        /// </summary>
        private async UniTask<string> ReadTextAsync(string filePath)
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
                return string.Empty;
            });
        }

        /// <summary>
        /// 异步写入文本
        /// </summary>
        private async UniTask WriteTextAsync(string filePath, string content)
        {
            await UniTask.RunOnThreadPool(() => File.WriteAllText(filePath, content));
        }

        #endregion
    }
}
