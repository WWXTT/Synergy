using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 效果定义存储管理器
    /// 负责效果的保存、加载、删除和导入导出
    /// </summary>
    public class EffectDefinitionStorage
    {
        #region 单例

        private static EffectDefinitionStorage _instance;
        public static EffectDefinitionStorage Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EffectDefinitionStorage();
                }
                return _instance;
            }
        }

        #endregion

        #region 常量

        /// <summary>效果保存目录</summary>
        private const string EFFECTS_DIRECTORY = "Effects";

        /// <summary>效果文件扩展名</summary>
        private const string EFFECT_FILE_EXTENSION = ".json";

        /// <summary>效果库文件名</summary>
        private const string EFFECT_LIBRARY_FILE = "EffectLibrary.json";

        #endregion

        #region 字段

        /// <summary>缓存的效果列表</summary>
        private Dictionary<string, EffectDefinitionData> _cachedEffects = new Dictionary<string, EffectDefinitionData>();

        /// <summary>是否已加载</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>保存路径</summary>
        private string _savePath;

        #endregion

        #region 构造函数

        private EffectDefinitionStorage()
        {
            // 确定保存路径
            _savePath = Path.Combine(Application.persistentDataPath, EFFECTS_DIRECTORY);

            // 确保目录存在
            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 保存效果到本地
        /// </summary>
        /// <param name="effect">效果数据</param>
        /// <returns>是否成功</returns>
        public async UniTask<bool> SaveEffectAsync(EffectDefinitionData effect)
        {
            if (effect == null || string.IsNullOrEmpty(effect.Id))
            {
                Debug.LogError("EffectDefinitionStorage: 无效的效果数据");
                return false;
            }

            try
            {
                // 生成文件路径
                string filePath = GetEffectFilePath(effect.Id);

                // 序列化为JSON
                string json = JsonUtility.ToJson(effect, true);

                // 异步写入文件
                await WriteTextAsync(filePath, json);

                // 更新缓存
                _cachedEffects[effect.Id] = effect;

                Debug.Log($"EffectDefinitionStorage: 效果 {effect.DisplayName} 保存成功");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 保存效果失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量保存效果
        /// </summary>
        /// <param name="effects">效果列表</param>
        /// <returns>成功保存的数量</returns>
        public async UniTask<int> SaveEffectsAsync(List<EffectDefinitionData> effects)
        {
            if (effects == null || effects.Count == 0) return 0;

            int successCount = 0;

            foreach (var effect in effects)
            {
                if (await SaveEffectAsync(effect))
                {
                    successCount++;
                }
            }

            return successCount;
        }

        /// <summary>
        /// 加载所有效果
        /// </summary>
        /// <returns>效果列表</returns>
        public async UniTask<List<EffectDefinitionData>> LoadAllEffectsAsync()
        {
            var result = new List<EffectDefinitionData>();

            try
            {
                // 清空缓存
                _cachedEffects.Clear();

                // 获取所有效果文件
                string[] files = Directory.GetFiles(_savePath, $"*{EFFECT_FILE_EXTENSION}");

                foreach (string file in files)
                {
                    try
                    {
                        // 异步读取文件
                        string json = await ReadTextAsync(file);

                        if (!string.IsNullOrEmpty(json))
                        {
                            var effect = JsonUtility.FromJson<EffectDefinitionData>(json);

                            if (effect != null && !string.IsNullOrEmpty(effect.Id))
                            {
                                result.Add(effect);
                                _cachedEffects[effect.Id] = effect;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"EffectDefinitionStorage: 加载效果文件 {file} 失败 - {ex.Message}");
                    }
                }

                IsLoaded = true;
                Debug.Log($"EffectDefinitionStorage: 加载了 {result.Count} 个效果");
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 加载效果失败 - {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 加载指定效果
        /// </summary>
        /// <param name="effectId">效果ID</param>
        /// <returns>效果数据</returns>
        public async UniTask<EffectDefinitionData> LoadEffectAsync(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return null;

            // 先检查缓存
            if (_cachedEffects.TryGetValue(effectId, out var cachedEffect))
            {
                return cachedEffect;
            }

            try
            {
                string filePath = GetEffectFilePath(effectId);
                string json = await ReadTextAsync(filePath);

                if (!string.IsNullOrEmpty(json))
                {
                    var effect = JsonUtility.FromJson<EffectDefinitionData>(json);

                    if (effect != null)
                    {
                        _cachedEffects[effectId] = effect;
                        return effect;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"EffectDefinitionStorage: 加载效果 {effectId} 失败 - {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 删除效果
        /// </summary>
        /// <param name="effectId">效果ID</param>
        /// <returns>是否成功</returns>
        public async UniTask<bool> DeleteEffectAsync(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return false;

            try
            {
                string filePath = GetEffectFilePath(effectId);

                if (File.Exists(filePath))
                {
                    // 异步删除文件
                    await UniTask.RunOnThreadPool(() => File.Delete(filePath));
                }

                // 从缓存移除
                _cachedEffects.Remove(effectId);

                Debug.Log($"EffectDefinitionStorage: 效果 {effectId} 已删除");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 删除效果失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查效果是否存在
        /// </summary>
        /// <param name="effectId">效果ID</param>
        /// <returns>是否存在</returns>
        public bool EffectExists(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return false;

            // 检查缓存
            if (_cachedEffects.ContainsKey(effectId)) return true;

            // 检查文件
            string filePath = GetEffectFilePath(effectId);
            return File.Exists(filePath);
        }

        /// <summary>
        /// 获取缓存的效果列表
        /// </summary>
        /// <returns>效果列表</returns>
        public List<EffectDefinitionData> GetCachedEffects()
        {
            return new List<EffectDefinitionData>(_cachedEffects.Values);
        }

        /// <summary>
        /// 从缓存获取效果
        /// </summary>
        /// <param name="effectId">效果ID</param>
        /// <returns>效果数据</returns>
        public EffectDefinitionData GetCachedEffect(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return null;

            _cachedEffects.TryGetValue(effectId, out var effect);
            return effect;
        }

        #endregion

        #region 导入导出

        /// <summary>
        /// 导出效果为JSON
        /// </summary>
        /// <param name="effect">效果数据</param>
        /// <returns>JSON字符串</returns>
        public string ExportToJson(EffectDefinitionData effect)
        {
            if (effect == null) return string.Empty;

            try
            {
                return JsonUtility.ToJson(effect, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 导出效果失败 - {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 批量导出效果为JSON
        /// </summary>
        /// <param name="effects">效果列表</param>
        /// <returns>JSON字符串</returns>
        public string ExportAllToJson(List<EffectDefinitionData> effects)
        {
            if (effects == null || effects.Count == 0) return string.Empty;

            try
            {
                var wrapper = new EffectDefinitionDataList { Effects = effects };
                return JsonUtility.ToJson(wrapper, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 批量导出效果失败 - {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从JSON导入效果
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>效果数据</returns>
        public EffectDefinitionData ImportFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                // 尝试解析单个效果
                var effect = JsonUtility.FromJson<EffectDefinitionData>(json);

                if (effect != null && !string.IsNullOrEmpty(effect.Id))
                {
                    return effect;
                }

                // 尝试解析效果列表
                var wrapper = JsonUtility.FromJson<EffectDefinitionDataList>(json);

                if (wrapper != null && wrapper.Effects != null && wrapper.Effects.Count > 0)
                {
                    return wrapper.Effects[0];
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 导入效果失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从JSON批量导入效果
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>效果列表</returns>
        public List<EffectDefinitionData> ImportAllFromJson(string json)
        {
            var result = new List<EffectDefinitionData>();

            if (string.IsNullOrEmpty(json)) return result;

            try
            {
                // 尝试解析效果列表
                var wrapper = JsonUtility.FromJson<EffectDefinitionDataList>(json);

                if (wrapper != null && wrapper.Effects != null)
                {
                    result.AddRange(wrapper.Effects);
                }
                else
                {
                    // 尝试解析单个效果
                    var effect = JsonUtility.FromJson<EffectDefinitionData>(json);

                    if (effect != null)
                    {
                        result.Add(effect);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 批量导入效果失败 - {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 导出效果到文件
        /// </summary>
        /// <param name="effect">效果数据</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功</returns>
        public async UniTask<bool> ExportToFileAsync(EffectDefinitionData effect, string filePath)
        {
            if (effect == null || string.IsNullOrEmpty(filePath)) return false;

            try
            {
                string json = ExportToJson(effect);
                await WriteTextAsync(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 导出效果到文件失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件导入效果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>效果数据</returns>
        public async UniTask<EffectDefinitionData> ImportFromFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            try
            {
                string json = await ReadTextAsync(filePath);
                return ImportFromJson(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"EffectDefinitionStorage: 从文件导入效果失败 - {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 生成新的效果ID
        /// </summary>
        /// <returns>唯一效果ID</returns>
        public string GenerateEffectId()
        {
            string id;
            do
            {
                id = $"effect_{DateTime.Now.Ticks % 1000000}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            } while (EffectExists(id));

            return id;
        }

        /// <summary>
        /// 获取效果文件路径
        /// </summary>
        /// <param name="effectId">效果ID</param>
        /// <returns>文件路径</returns>
        private string GetEffectFilePath(string effectId)
        {
            // 清理文件名中的非法字符
            string safeId = string.Join("_", effectId.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_savePath, $"{safeId}{EFFECT_FILE_EXTENSION}");
        }

        /// <summary>
        /// 异步写入文本
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">内容</param>
        private async UniTask WriteTextAsync(string filePath, string content)
        {
            await UniTask.RunOnThreadPool(() => File.WriteAllText(filePath, content));
        }

        /// <summary>
        /// 异步读取文本
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文本内容</returns>
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
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _cachedEffects.Clear();
            IsLoaded = false;
        }

        /// <summary>
        /// 获取保存路径
        /// </summary>
        /// <returns>保存路径</returns>
        public string GetSavePath()
        {
            return _savePath;
        }

        #endregion
    }
}