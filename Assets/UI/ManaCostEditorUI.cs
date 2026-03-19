using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using cfg;
using CardCore.Data;

namespace CardCore.UI
{
    /// <summary>
    /// 法力消耗编辑器 UI - 用于设置卡牌的法力消耗
    /// </summary>
    public class ManaCostEditorUI : MonoBehaviour
    {
        [Header("法力类型输入")]
        [SerializeField] private ManaCostInputUI _grayManaInput;
        [SerializeField] private ManaCostInputUI _redManaInput;
        [SerializeField] private ManaCostInputUI _blueManaInput;
        [SerializeField] private ManaCostInputUI _greenManaInput;
        [SerializeField] private ManaCostInputUI _whiteManaInput;
        [SerializeField] private ManaCostInputUI _blackManaInput;

        [Header("总费用显示")]
        [SerializeField] private TextMeshProUGUI _totalCostText;
        [SerializeField] private Image _totalCostBackground;

        [Header("按钮")]
        [SerializeField] private Button _clearButton;
        [SerializeField] private Button _presetButton;

        [Header("颜色配置")]
        [SerializeField] private Color _grayManaColor = new Color(0.61f, 0.66f, 0.66f);
        [SerializeField] private Color _redManaColor = new Color(1f, 0.27f, 0.27f);
        [SerializeField] private Color _blueManaColor = new Color(0.27f, 0.53f, 1f);
        [SerializeField] private Color _greenManaColor = new Color(0.27f, 0.67f, 0.27f);
        [SerializeField] private Color _whiteManaColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color _blackManaColor = new Color(0.2f, 0.2f, 0.2f);

        [Header("费用限制配置")]
        [SerializeField] private float _maxSingleManaCost = 5f;
        [SerializeField] private float _maxTotalCost = 15f;

        // 法力输入数组
        private ManaCostInputUI[] _manaInputs;

        // 法力类型枚举映射
        private static readonly ManaType[] ManaTypeOrder = new ManaType[]
        {
            ManaType.灰色, ManaType.红色, ManaType.蓝色, ManaType.绿色, ManaType.白色, ManaType.黑色
        };

        // 费用改变事件
        public event Action<Dictionary<int, float>> OnCostChanged;

        private void Awake()
        {
            InitializeManaInputs();
            BindEvents();
            UpdateTotalCost();
        }

        /// <summary>
        /// 初始化法力输入
        /// </summary>
        private void InitializeManaInputs()
        {
            _manaInputs = new ManaCostInputUI[]
            {
                _grayManaInput,
                _redManaInput,
                _blueManaInput,
                _greenManaInput,
                _whiteManaInput,
                _blackManaInput
            };

            // 设置法力类型和颜色
            for (int i = 0; i < _manaInputs.Length && i < ManaTypeOrder.Length; i++)
            {
                if (_manaInputs[i] != null)
                {
                    _manaInputs[i].SetManaType(ManaTypeOrder[i]);
                    _manaInputs[i].SetManaTypeColor(GetManaTypeColor(ManaTypeOrder[i]));
                    _manaInputs[i].SetMaxCost(_maxSingleManaCost);
                }
            }
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            _clearButton?.AddClickListener(OnClearClicked);
            _presetButton?.AddClickListener(OnPresetClicked);

            // 绑定每个法力输入的事件
            foreach (var input in _manaInputs)
            {
                if (input != null)
                {
                    input.OnValueChanged = OnManaValueChanged;
                }
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
                    return _grayManaColor;
                case ManaType.红色:
                    return _redManaColor;
                case ManaType.蓝色:
                    return _blueManaColor;
                case ManaType.绿色:
                    return _greenManaColor;
                case ManaType.白色:
                    return _whiteManaColor;
                case ManaType.黑色:
                    return _blackManaColor;
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 设置费用
        /// </summary>
        public void SetCost(Dictionary<int, float> cost)
        {
            if (cost == null) return;

            for (int i = 0; i < _manaInputs.Length && i < ManaTypeOrder.Length; i++)
            {
                int manaTypeInt = (int)ManaTypeOrder[i];
                if (cost.TryGetValue(manaTypeInt, out float value))
                {
                    _manaInputs[i]?.SetCost(value);
                }
                else
                {
                    _manaInputs[i]?.SetCost(0);
                }
            }

            UpdateTotalCost();
        }

        /// <summary>
        /// 获取费用
        /// </summary>
        public Dictionary<int, float> GetCost()
        {
            var cost = new Dictionary<int, float>();

            for (int i = 0; i < _manaInputs.Length && i < ManaTypeOrder.Length; i++)
            {
                float value = _manaInputs[i]?.GetCost() ?? 0;
                if (value > 0)
                {
                    cost[(int)ManaTypeOrder[i]] = value;
                }
            }

            return cost;
        }

        /// <summary>
        /// 获取总费用
        /// </summary>
        public float GetTotalCost()
        {
            float total = 0;
            foreach (var input in _manaInputs)
            {
                if (input != null)
                {
                    total += input.GetCost();
                }
            }
            return total;
        }

        /// <summary>
        /// 清除所有费用
        /// </summary>
        public void ClearCost()
        {
            foreach (var input in _manaInputs)
            {
                input?.SetCost(0);
            }
            UpdateTotalCost();
        }

        /// <summary>
        /// 法力值改变时调用
        /// </summary>
        private void OnManaValueChanged()
        {
            UpdateTotalCost();
            OnCostChanged?.Invoke(GetCost());
        }

        /// <summary>
        /// 更新总费用显示
        /// </summary>
        private void UpdateTotalCost()
        {
            float totalCost = GetTotalCost();

            if (_totalCostText != null)
            {
                _totalCostText.text = totalCost.ToString("F1");
            }

            if (_totalCostBackground != null)
            {
                // 根据费用设置背景颜色
                if (totalCost > _maxTotalCost)
                {
                    _totalCostBackground.color = new Color(1f, 0.3f, 0.3f); // 红色警告
                }
                else if (totalCost > _maxTotalCost * 0.8f)
                {
                    _totalCostBackground.color = new Color(1f, 0.8f, 0.3f); // 黄色警告
                }
                else
                {
                    _totalCostBackground.color = new Color(0.3f, 0.8f, 1f); // 蓝色正常
                }
            }
        }

        /// <summary>
        /// 清除按钮点击
        /// </summary>
        private void OnClearClicked()
        {
            ClearCost();
        }

        /// <summary>
        /// 预设按钮点击
        /// </summary>
        private void OnPresetClicked()
        {
            // TODO: 显示预设费用选择对话框
            ShowCostPresetDialog();
        }

        /// <summary>
        /// 显示费用预设对话框
        /// </summary>
        private void ShowCostPresetDialog()
        {
            // 预定义的费用模式
            var presets = new[]
            {
                new CostPreset { Name = "低消耗", Cost = new Dictionary<int, float> { { 1, 1 } } },
                new CostPreset { Name = "中消耗", Cost = new Dictionary<int, float> { { 1, 3 } } },
                new CostPreset { Name = "高消耗", Cost = new Dictionary<int, float> { { 1, 5 } } },
                new CostPreset { Name = "双色", Cost = new Dictionary<int, float> { { 2, 2 }, { 3, 2 } } },
                new CostPreset { Name = "三色", Cost = new Dictionary<int, float> { { 1, 1 }, { 2, 1 }, { 3, 2 } } }
            };

            // TODO: 显示预设选择 UI
            UIManager.Instance.ShowNotification("预设功能开发中...");
        }

        /// <summary>
        /// 验证费用是否有效
        /// </summary>
        public bool ValidateCost(out string errorMessage)
        {
            float totalCost = GetTotalCost();

            // 检查总费用
            if (totalCost > _maxTotalCost)
            {
                errorMessage = $"总费用不能超过 {_maxTotalCost}（当前：{totalCost}）";
                return false;
            }

            if (totalCost <= 0)
            {
                errorMessage = "卡牌必须至少消耗 1 点法力";
                return false;
            }

            // 检查单色费用
            foreach (var input in _manaInputs)
            {
                if (input != null)
                {
                    float cost = input.GetCost();
                    if (cost > _maxSingleManaCost)
                    {
                        errorMessage = $"单色 {input.ManaType} 法力消耗不能超过 {_maxSingleManaCost}（当前：{cost}）";
                        return false;
                    }
                }
            }

            errorMessage = null;
            return true;
        }
    }

    /// <summary>
    /// 法力输入组件
    /// </summary>
    public class ManaCostInputUI : MonoBehaviour
    {
        [Header("UI 元素")]
        [SerializeField] private TMP_InputField _costInput;
        [SerializeField] private Button _incrementButton;
        [SerializeField] private Button _decrementButton;
        [SerializeField] private Image _manaIcon;
        [SerializeField] private TextMeshProUGUI _manaTypeText;

        [Header("配置")]
        [SerializeField] private float _minCost = 0;
        [SerializeField] private float _maxCost = 5;
        [SerializeField] private float _step = 0.5f;

        private ManaType _manaType;
        private float _currentCost = 0;

        public ManaType ManaType => _manaType;
        public Action OnValueChanged;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Initialize()
        {
            // 绑定按钮事件
            _incrementButton?.AddClickListener(OnIncrementClicked);
            _decrementButton?.AddClickListener(OnDecrementClicked);

            // 绑定输入事件
            _costInput?.onValueChanged.AddListener(OnInputChanged);
            _costInput?.onEndEdit.AddListener(OnInputSubmitted);

            // 设置初始值
            UpdateDisplay();
        }

        /// <summary>
        /// 设置法力类型
        /// </summary>
        public void SetManaType(ManaType manaType)
        {
            _manaType = manaType;

            if (_manaTypeText != null)
            {
                _manaTypeText.text = manaType.ToString();
            }
        }

        /// <summary>
        /// 设置法力类型颜色
        /// </summary>
        public void SetManaTypeColor(Color color)
        {
            if (_manaIcon != null)
            {
                _manaIcon.color = color;
            }
        }

        /// <summary>
        /// 设置最大费用
        /// </summary>
        public void SetMaxCost(float maxCost)
        {
            _maxCost = maxCost;

            // 确保当前值不超过最大值
            if (_currentCost > _maxCost)
            {
                _currentCost = _maxCost;
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 设置费用
        /// </summary>
        public void SetCost(float cost)
        {
            _currentCost = Mathf.Clamp(cost, _minCost, _maxCost);
            UpdateDisplay();
        }

        /// <summary>
        /// 获取费用
        /// </summary>
        public float GetCost()
        {
            return _currentCost;
        }

        /// <summary>
        /// 增加费用
        /// </summary>
        private void OnIncrementClicked()
        {
            float newCost = Mathf.Round((_currentCost + _step) * 2) / 2;
            SetCost(newCost);
            TriggerValueChanged();
        }

        /// <summary>
        /// 减少费用
        /// </summary>
        private void OnDecrementClicked()
        {
            float newCost = Mathf.Round((_currentCost - _step) * 2) / 2;
            SetCost(newCost);
            TriggerValueChanged();
        }

        /// <summary>
        /// 输入改变
        /// </summary>
        private void OnInputChanged(string value)
        {
            // 实时输入时不更新，等待提交
        }

        /// <summary>
        /// 输入提交
        /// </summary>
        private void OnInputSubmitted(string value)
        {
            if (float.TryParse(value, out float cost))
            {
                SetCost(cost);
                TriggerValueChanged();
            }
        }

        /// <summary>
        /// 更新显示
        /// </summary>
        private void UpdateDisplay()
        {
            if (_costInput != null)
            {
                _costInput.text = _currentCost > 0 ? _currentCost.ToString("F1") : string.Empty;
            }
        }

        /// <summary>
        /// 触发值改变事件
        /// </summary>
        private void TriggerValueChanged()
        {
            OnValueChanged?.Invoke();
        }
    }

    /// <summary>
    /// 费用预设
    /// </summary>
    [Serializable]
    public class CostPreset
    {
        public string Name;
        public Dictionary<int, float> Cost;
    }
}
