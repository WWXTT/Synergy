using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using Cysharp.Threading.Tasks;
using cfg;
using CardCore.Data;

namespace CardCore.UI
{
    /// <summary>
    /// 效果编辑器 UI - 用于添加和编辑卡牌效果
    /// </summary>
    public class EffectEditorUI : BaseUI
    {
        [Header("效果选择")]
        [SerializeField] private TMP_Dropdown _effectAbbreviationDropdown;
        [SerializeField] private TMP_Dropdown _effectSpeedDropdown;
        [SerializeField] private TMP_Dropdown _manaTypeDropdown;

        [Header("效果参数")]
        [SerializeField] private TMP_InputField _parametersInput;
        [SerializeField] private Toggle _initiativeToggle;

        [Header("效果描述")]
        [SerializeField] private TMP_InputField _descriptionInput;
        [SerializeField] private TextMeshProUGUI _descriptionCountText;

        [Header("效果列表")]
        [SerializeField] private Transform _effectsContainer;
        [SerializeField] private GameObject _effectItemPrefab;
        [SerializeField] private ScrollRect _effectsScrollRect;

        [Header("按钮")]
        [SerializeField] private Button _addEffectButton;
        [SerializeField] private Button _clearEffectsButton;

        [Header("提示信息")]
        [SerializeField] private TextMeshProUGUI _effectNameText;
        [SerializeField] private TextMeshProUGUI _effectDescriptionText;

        // 当前编辑的效果列表
        private List<EffectData> _currentEffects = new List<EffectData>();
        private List<EffectEditorItemUI> _effectItems = new List<EffectEditorItemUI>();


        // 效果数据缓存
        private List<EffectEntry> _availableEffects = new List<EffectEntry>();

        // 效果改变事件
        public event Action<List<EffectData>> OnEffectsChanged;

        protected override void Initialize()
        {
            base.Initialize();

            // 加载效果逻辑库
            LoadEffectLibrary();

            // 初始化下拉菜单
            InitializeDropdowns();

            // 绑定按钮事件
            _addEffectButton?.AddClickListener(OnAddEffectClicked);
            _clearEffectsButton?.AddClickListener(OnClearEffectsClicked);

            // 绑定输入事件
            _parametersInput?.onValueChanged.AddListener(OnParameterChanged);
            _descriptionInput?.onValueChanged.AddListener(OnDescriptionChanged);

            // 更新描述计数
            UpdateDescriptionCount();
        }

        /// <summary>
        /// 加载效果逻辑库
        /// </summary>
        private void LoadEffectLibrary()
        {

            CreateDefaultEffects();
        }

        /// <summary>
        /// 创建默认效果列表
        /// </summary>
        private void CreateDefaultEffects()
        {
            _availableEffects.Clear();

            // 预定义效果列表
            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "DMG",
                Description = "对目标造成 {P} 点伤害",
                ManaType = ManaType.红色,
                DefaultParameter = 1
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "HEAL",
                Description = "回复目标 {P} 点生命",
                ManaType = ManaType.绿色,
                DefaultParameter = 1
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "DRAW",
                Description = "抽 {P} 张卡",
                ManaType = ManaType.蓝色,
                DefaultParameter = 1
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "BUFF_ATK",
                Description = "目标攻击力 +{P}",
                ManaType = ManaType.白色,
                DefaultParameter = 1
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "BUFF_DEF",
                Description = "目标生命值 +{P}",
                ManaType = ManaType.白色,
                DefaultParameter = 1
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "DEBUFF_ATK",
                Description = "目标攻击力 -{P}",
                ManaType = ManaType.黑色,
                DefaultParameter = 1
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "DEBUFF_DEF",
                Description = "目标生命值 -{P}",
                ManaType = ManaType.黑色,
                DefaultParameter = 1
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "NEGATE",
                Description = "无效一个效果",
                ManaType = ManaType.蓝色,
                DefaultParameter = 0
            });

            _availableEffects.Add(new EffectEntry
            {
                Abbreviation = "DESTROY",
                Description = "销毁一张卡",
                ManaType = ManaType.黑色,
                DefaultParameter = 0
            });
        }

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            // 效果缩写下拉菜单
            if (_effectAbbreviationDropdown != null)
            {
                _effectAbbreviationDropdown.ClearOptions();

                List<string> options = new List<string> { "选择效果..." };
                foreach (var effect in _availableEffects)
                {
                    options.Add($"{effect.Abbreviation} - {effect.Description}");
                }

                _effectAbbreviationDropdown.AddOptions(options);
                _effectAbbreviationDropdown.value = 0;

                _effectAbbreviationDropdown.onValueChanged.AddListener(OnEffectAbbreviationChanged);
            }

            // 效果速度下拉菜单
            if (_effectSpeedDropdown != null)
            {
                _effectSpeedDropdown.ClearOptions();

                List<string> speedOptions = new List<string>();
                foreach (EffectSpeed speed in Enum.GetValues(typeof(EffectSpeed)))
                {
                    speedOptions.Add(GetEffectSpeedDisplayName(speed));
                }

                _effectSpeedDropdown.AddOptions(speedOptions);
                _effectSpeedDropdown.value = 0;
            }

            // 法力类型下拉菜单
            if (_manaTypeDropdown != null)
            {
                _manaTypeDropdown.ClearOptions();

                List<string> manaOptions = new List<string>();
                foreach (ManaType mana in Enum.GetValues(typeof(ManaType)))
                {
                    manaOptions.Add(mana.ToString());
                }

                _manaTypeDropdown.AddOptions(manaOptions);
                _manaTypeDropdown.value = 0;
            }
        }

        /// <summary>
        /// 获取效果速度显示名称
        /// </summary>
        private string GetEffectSpeedDisplayName(EffectSpeed speed)
        {
            switch (speed)
            {
                case EffectSpeed.强制诱发:
                    return "强制诱发";
                case EffectSpeed.可选诱发:
                    return "可选诱发";
                case EffectSpeed.自由时点:
                    return "自由时点";
                default:
                    return speed.ToString();
            }
        }

        /// <summary>
        /// 设置当前效果列表
        /// </summary>
        public void SetEffects(List<EffectData> effects)
        {
            _currentEffects.Clear();
            if (effects != null)
            {
                _currentEffects.AddRange(effects);
            }

            RefreshEffectsList();
        }

        /// <summary>
        /// 获取当前效果列表
        /// </summary>
        public List<EffectData> GetEffects()
        {
            return new List<EffectData>(_currentEffects);
        }

        /// <summary>
        /// 刷新效果列表
        /// </summary>
        private void RefreshEffectsList()
        {
            // 清除现有的效果编辑器项
            foreach (var item in _effectItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
            }
            _effectItems.Clear();

            // 创建新的效果编辑器项
            foreach (var effect in _currentEffects)
            {
                CreateEffectItem(effect);
            }
        }

        /// <summary>
        /// 创建效果项
        /// </summary>
        private void CreateEffectItem(EffectData effect)
        {
            if (_effectItemPrefab == null || _effectsContainer == null) return;

            GameObject itemObj = Instantiate(_effectItemPrefab, _effectsContainer);
            var itemUI = itemObj.GetComponent<EffectEditorItemUI>();

            if (itemUI != null)
            {
                itemUI.Initialize(effect, OnEffectItemChanged, OnEffectItemRemoved);
                _effectItems.Add(itemUI);
            }
        }

        // ==================== 事件处理 ====================

        /// <summary>
        /// 效果缩写下拉菜单改变
        /// </summary>
        private void OnEffectAbbreviationChanged(int index)
        {
            if (index <= 0) return;

            // 找到对应的效果数据
            int effectIndex = index - 1;
            if (effectIndex >= 0 && effectIndex < _availableEffects.Count)
            {
                var effectData = _availableEffects[effectIndex];

                // 更新显示
                if (_effectNameText != null)
                {
                    _effectNameText.text = effectData.Abbreviation;
                }

                if (_effectDescriptionText != null)
                {
                    _effectDescriptionText.text = effectData.Description;
                }

                // 设置默认值
                if (_parametersInput != null)
                {
                    _parametersInput.text = effectData.DefaultParameter.ToString();
                }

                if (_manaTypeDropdown != null)
                {
                    _manaTypeDropdown.value = (int)effectData.ManaType;
                }

                // 更新描述输入框中的占位符
                string descriptionWithParameter = effectData.Description.Replace("{P}", "参数值");
                if (_descriptionInput != null && _descriptionInput.placeholder != null)
                {
                    _descriptionInput.placeholder.GetComponent<TextMeshProUGUI>().text = descriptionWithParameter;
                }
            }
        }

        /// <summary>
        /// 参数输入改变
        /// </summary>
        private void OnParameterChanged(string value)
        {
            // 参数改变时更新描述中的占位符显示
            if (_descriptionInput != null && _descriptionInput.placeholder != null)
            {
                string currentDescription = _descriptionInput.placeholder.GetComponent<TextMeshProUGUI>().text;
                // 可以在这里实时更新描述中的参数值
            }
        }

        /// <summary>
        /// 描述输入改变
        /// </summary>
        private void OnDescriptionChanged(string value)
        {
            UpdateDescriptionCount();
        }

        /// <summary>
        /// 更新描述字符计数
        /// </summary>
        private void UpdateDescriptionCount()
        {
            if (_descriptionCountText != null && _descriptionInput != null)
            {
                int count = _descriptionInput.text?.Length ?? 0;
                _descriptionCountText.text = $"{count}/200";
                _descriptionCountText.color = count > 200 ? Color.red : Color.white;
            }
        }

        /// <summary>
        /// 添加效果按钮点击
        /// </summary>
        private void OnAddEffectClicked()
        {
            // 检查是否选择了效果
            if (_effectAbbreviationDropdown.value <= 0)
            {
                UIManager.Instance.ShowNotification("请先选择一个效果");
                return;
            }

            // 获取选择的索引
            int effectIndex = _effectAbbreviationDropdown.value - 1;
            if (effectIndex < 0 || effectIndex >= _availableEffects.Count)
            {
                return;
            }

            var effectData = _availableEffects[effectIndex];

            // 创建新效果
            var newEffect = new EffectData
            {
                Abbreviation = effectData.Abbreviation,
                Initiative = _initiativeToggle?.isOn ?? true,
                Speed = (EffectSpeed)_effectSpeedDropdown.value,
                ManaType = (ManaType)_manaTypeDropdown.value,
                Description = _descriptionInput?.text ?? string.Empty
            };

            // 解析参数
            if (float.TryParse(_parametersInput?.text, out float paramValue))
            {
                newEffect.Parameters = paramValue;
            }

            // 检查是否已存在相同缩写的效果
            bool exists = false;
            foreach (var effect in _currentEffects)
            {
                if (effect.Abbreviation == newEffect.Abbreviation)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
            {
                UIManager.Instance.ShowNotification($"效果 {newEffect.Abbreviation} 已存在");
                return;
            }

            // 添加效果
            _currentEffects.Add(newEffect);
            CreateEffectItem(newEffect);

            // 触发事件
            OnEffectsChanged?.Invoke(_currentEffects);

            // 自动滚动到列表底部
            ScrollToBottomAsync().Forget();
        }

        /// <summary>
        /// 清除所有效果按钮点击
        /// </summary>
        private void OnClearEffectsClicked()
        {
            // TODO: 显示确认对话框
            _currentEffects.Clear();
            RefreshEffectsList();
            OnEffectsChanged?.Invoke(_currentEffects);
            UIManager.Instance.ShowNotification("已清除所有效果");
        }

        /// <summary>
        /// 效果项改变
        /// </summary>
        private void OnEffectItemChanged(EffectData effect)
        {
            OnEffectsChanged?.Invoke(_currentEffects);
        }

        /// <summary>
        /// 效果项移除
        /// </summary>
        private void OnEffectItemRemoved(EffectData effect)
        {
            _currentEffects.Remove(effect);
            _effectItems.RemoveAll(item => item.Effect == effect);
            OnEffectsChanged?.Invoke(_currentEffects);
        }

        /// <summary>
        /// 滚动到列表底部
        /// </summary>
        private async UniTaskVoid ScrollToBottomAsync()
        {
            await UniTask.WaitForEndOfFrame(this);
            if (_effectsScrollRect != null)
            {
                _effectsScrollRect.normalizedPosition = Vector2.zero;
            }
        }

        protected override void OnHide()
        {
            base.OnHide();
            // 隐藏时重置选择
            if (_effectAbbreviationDropdown != null)
            {
                _effectAbbreviationDropdown.value = 0;
            }
            if (_effectNameText != null)
            {
                _effectNameText.text = string.Empty;
            }
            if (_effectDescriptionText != null)
            {
                _effectDescriptionText.text = string.Empty;
            }
        }

        private void OnDestroy()
        {
            // 清理效果项
            foreach (var item in _effectItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
            }
            _effectItems.Clear();
        }
    }

    /// <summary>
    /// 效果编辑器项 UI
    /// </summary>
    public class EffectEditorItemUI : MonoBehaviour
    {
        [Header("效果显示")]
        [SerializeField] private TextMeshProUGUI _abbreviationText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _speedText;
        [SerializeField] private Image _manaTypeIcon;

        [Header("按钮")]
        [SerializeField] private Button _editButton;
        [SerializeField] private Button _removeButton;

        private EffectData _effect;
        private Action<EffectData> _onChanged;
        private Action<EffectData> _onRemoved;

        public EffectData Effect => _effect;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(EffectData effect, Action<EffectData> onChanged, Action<EffectData> onRemoved)
        {
            _effect = effect;
            _onChanged = onChanged;
            _onRemoved = onRemoved;

            // 绑定按钮事件
            _editButton?.AddClickListener(OnEditClicked);
            _removeButton?.AddClickListener(OnRemoveClicked);

            UpdateUI();
        }

        /// <summary>
        /// 更新 UI
        /// </summary>
        private void UpdateUI()
        {
            if (_effect == null) return;

            if (_abbreviationText != null)
            {
                _abbreviationText.text = _effect.Abbreviation ?? "未知";
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = _effect.Description ?? string.Empty;
            }

            if (_speedText != null)
            {
                _speedText.text = GetEffectSpeedDisplayName(_effect.Speed);
            }

            if (_manaTypeIcon != null)
            {
                _manaTypeIcon.color = GetManaTypeColor(_effect.ManaType);
            }
        }

        /// <summary>
        /// 获取效果速度显示名称
        /// </summary>
        private string GetEffectSpeedDisplayName(EffectSpeed speed)
        {
            switch (speed)
            {
                case EffectSpeed.强制诱发:
                    return "强制";
                case EffectSpeed.可选诱发:
                    return "诱发";
                case EffectSpeed.自由时点:
                    return "自由";
                default:
                    return speed.ToString();
            }
        }

        /// <summary>
        /// 获取法力类型颜色
        /// </summary>
        private Color GetManaTypeColor(ManaType manaType)
        {
            switch (manaType)
            {
                case ManaType.灰色:
                    return new Color(0.61f, 0.66f, 0.66f);
                case ManaType.红色:
                    return new Color(1f, 0.27f, 0.27f);
                case ManaType.蓝色:
                    return new Color(0.27f, 0.53f, 1f);
                case ManaType.绿色:
                    return new Color(0.27f, 0.67f, 0.27f);
                case ManaType.白色:
                    return new Color(1f, 1f, 1f);
                case ManaType.黑色:
                    return new Color(0.2f, 0.2f, 0.2f);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 编辑按钮点击
        /// </summary>
        private void OnEditClicked()
        {
            // TODO: 打开效果编辑对话框
            _onChanged?.Invoke(_effect);
        }

        /// <summary>
        /// 移除按钮点击
        /// </summary>
        private void OnRemoveClicked()
        {
            _onRemoved?.Invoke(_effect);
        }
    }

    /// <summary>
    /// 效果条目（简化版）
    /// </summary>
    [Serializable]
    public class EffectEntry
    {
        public string Abbreviation;
        public string Description;
        public ManaType ManaType;
        public float DefaultParameter;
    }
}
