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
    /// 卡牌参数面板UI
    /// 栌据卡牌类型动态显示/隐藏参数��入项
    /// </summary>
    public class CardParameterPanelUI : BaseUI
    {
        #region 序列化字段

        [Header("卡牌类型")]
        [SerializeField] private TMP_Dropdown _cardTypeDropdown;

        [Header("基础参数")]
        [SerializeField] private GameObject _namePanel;
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TextMeshProUGUI _namePlaceholder;

        [Header("Monster/Legend参数")]
        [SerializeField] private GameObject _monsterLegendPanel;
        [SerializeField] private TMP_InputField _powerInput;
        [SerializeField] private TMP_InputField _lifeInput;
        [SerializeField] private TextMeshProUGUI _powerLabel;
        [SerializeField] private TextMeshProUGUI _lifeLabel;

        [Header("Field参数")]
        [SerializeField] private GameObject _fieldPanel;
        [SerializeField] private TMP_InputField _durabilityInput;
        [SerializeField] private TextMeshProUGUI _durabilityLabel;

        [Header("效果列表")]
        [SerializeField] private Transform _effectsContainer;
        [SerializeField] private GameObject _effectItemPrefab;
        [SerializeField] private Button _openEffectBuilderButton;

        [Header("验证信息")]
        [SerializeField] private TextMeshProUGUI _validationMessage;
        [SerializeField] private Image _validationIcon;

        [Header("确认按钮")]
        [SerializeField] private Button _confirmButton;

        #endregion

        #region 私有字段

        /// <summary>当前卡牌数据</summary>
        private CardData _currentCardData;

        /// <summary>当前参数配置</summary>
        private CardParameterConfig _currentConfig;

        /// <summary>必填参数缓存</summary>
        private Dictionary<CardParameterType, string> _requiredParameterValues = new Dictionary<CardParameterType, string>();

        /// <summary>卡牌类型变更回调</summary>
        public event Action<CardType> OnCardTypeChanged;

        /// <summary>卡牌确认回调</summary>
        public event Action<CardData> OnCardConfirmed;

        #endregion

        #region 生命周期

        protected override void Initialize()
        {
            base.Initialize();

            // 初始化卡牌类型下拉菜单
            InitializeCardTypeDropdown();

            // 绑定事件
            BindEvents();

            // 创建新卡牌
            CreateNewCard();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置卡牌数据
        /// </summary>
        public void SetCardData(CardData cardData)
        {
            if (cardData == null) return;
            _currentCardData = cardData;
            _currentConfig = CardParameterConfig.GetConfig(cardData.CardType);

            UpdateUIForCardType();
            ValidateParameters();
        }

        /// <summary>
        /// 获取当前卡牌数据
        /// </summary>
        public CardData GetCardData()
        {
            return _currentCardData;
        }

        /// <summary>
        /// 添加效果
        /// </summary>
        public void AddEffect(EffectDefinitionData effect)
        {
            if (effect == null || _currentCardData == null) return;

            // 风险效果约束检查
            if (!_currentConfig.ValidateEffect(effect, out string error))
            {
                UIManager.Instance.ShowNotification(error);
                return;
            }

            // 添加效果到列表
            var effectData = new EffectData
            {
                Abbreviation = effect.Id,
                Initiative = effect.ActivationType == (int)EffectActivationType.Voluntary,
                Parameters = effect.BaseSpeed,
                Speed = (EffectSpeed)effect.ActivationType,
                ManaType = ManaType.灰色,
                Description = effect.Description ?? effect.DisplayName
            };

            _currentCardData.Effects.Add(effectData);
            CreateEffectItem(effectData);
            UpdateConfirmButtonState();
        }

        private void UpdateConfirmButtonState()
        {
            if (_currentCardData == null || _currentConfig == null) return;

            // 检查必填参数
            bool allRequiredFilled = true;
            var missingParams = new List<string>();
            foreach (var required in _currentConfig.RequiredParameters)
            {
                if (!_requiredParameterValues.TryGetValue(required.ParameterType, out var value) || string.IsNullOrEmpty(value))
                {
                    allRequiredFilled = false;
                    missingParams.Add(required.DisplayName);
                }
            }

            // 更新确认按钮状态
            if (_confirmButton != null)
            {
                _confirmButton.interactable = allRequiredFilled;
            }
            string message = allRequiredFilled ? "参数验证通过" : $"请填写: {string.Join(", ", missingParams)}";
            SetValidationState(allRequiredFilled, message);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化卡牌类型下拉菜单
        /// </summary>
        private void InitializeCardTypeDropdown()
        {
            if (_cardTypeDropdown == null) return;

            _cardTypeDropdown.ClearOptions();

            var options = new List<string>
            {
                "怪兽 (Monster)",
                "传奇 (Legend)",
                "术法 (Magic)",
                "领域 (Field)"
            };

            _cardTypeDropdown.AddOptions(options);
            _cardTypeDropdown.value = 0;
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            _cardTypeDropdown?.onValueChanged.AddListener(OnCardTypeDropdownChanged);
            _nameInput?.onValueChanged.AddListener(OnNameInputChanged);
            _powerInput?.onValueChanged.AddListener(OnPowerInputChanged);
            _lifeInput?.onValueChanged.AddListener(OnLifeInputChanged);
            _durabilityInput?.onValueChanged.AddListener(OnDurabilityInputChanged);
            _openEffectBuilderButton?.AddClickListener(OnOpenEffectBuilderClicked);
            _confirmButton?.AddClickListener(OnConfirmClicked);
        }

        /// <summary>
        /// 创建新卡牌
        /// </summary>
        private void CreateNewCard()
        {
            _currentCardData = new CardData
            {
                CardType = CardType.生物,
                CardName = string.Empty,
                Illustration = string.Empty,
                Effects = new List<EffectData>(),
                Cost = new Dictionary<int, float>()
            };

            _currentConfig = CardParameterConfig.GetConfig(CardType.生物);
            UpdateUIForCardType();
        }

        /// <summary>
        /// 卡牌类型变更
        /// </summary>
        private void OnCardTypeDropdownChanged(int index)
        {
            if (_currentCardData == null) return;

            _currentCardData.CardType = (CardType)index;
            _currentConfig = CardParameterConfig.GetConfig(_currentCardData.CardType);

            UpdateUIForCardType();
            ValidateParameters();

            OnCardTypeChanged?.Invoke(_currentCardData.CardType);
        }

        /// <summary>
        /// 更新卡牌类型相关的UI
        /// </summary>
        private void UpdateUIForCardType()
        {
            if (_currentCardData == null || _currentConfig == null) return;

            // 隐藏所有参数面板
            _namePanel?.SetActive(false);
            _monsterLegendPanel?.SetActive(false);
            _fieldPanel?.SetActive(false);

            // 清除必填参数缓存
            _requiredParameterValues.Clear();

            // 根据卡牌类型显示对应面板
            switch (_currentCardData.CardType)
            {
                case CardType.生物:
                case CardType.传奇:
                    _monsterLegendPanel?.SetActive(true);
                    _requiredParameterValues[CardParameterType.Power] = "";
                    _requiredParameterValues[CardParameterType.Life] = "";
                    break;

                case CardType.术法:
                    // Magic卡牌只需要效果（必填）
                    _requiredParameterValues[CardParameterType.Effects] = "";
                    break;

                case CardType.领域:
                    _fieldPanel?.SetActive(true);
                    _requiredParameterValues[CardParameterType.Effects] = "";
                    _requiredParameterValues[CardParameterType.Durability] = "";
                    break;
            }

            // 更新标签
            if (_powerLabel != null) _powerLabel.text = "攻击力";
            if (_lifeLabel != null) _lifeLabel.text = "生命值";
            if (_durabilityLabel != null) _durabilityLabel.text = "耐久度";

            // 更新占位符
            _namePlaceholder.text = "不填则使用哈希值";
        }

        /// <summary>
        /// 输入变更处理
        /// </summary>
        private void OnNameInputChanged(string value)
        {
            _currentCardData.CardName = value;
            ValidateParameters();
        }

        private void OnPowerInputChanged(string value)
        {
            if (int.TryParse(value, out int power))
            {
                _currentCardData.Power = power;
                _requiredParameterValues[CardParameterType.Power] = value;
            }
            else
            {
                _currentCardData.Power = null;
                _requiredParameterValues.Remove(CardParameterType.Power);
            }
            ValidateParameters();
        }

        private void OnLifeInputChanged(string value)
        {
            if (int.TryParse(value, out int life))
            {
                _currentCardData.Life = life;
                _requiredParameterValues[CardParameterType.Life] = value;
            }
            else
            {
                _currentCardData.Life = null;
                _requiredParameterValues.Remove(CardParameterType.Life);
            }
            ValidateParameters();
        }

        private void OnDurabilityInputChanged(string value)
        {
            if (int.TryParse(value, out int durability))
            {
                // Field的耐久度存储在Tag中
                if (!_currentCardData.Tags.Contains("Durability"))
                {
                    _currentCardData.Tags.Add("Durability");
                }
                _requiredParameterValues[CardParameterType.Durability] = value;
            }
            else
            {
                _requiredParameterValues.Remove(CardParameterType.Durability);
            }
            ValidateParameters();
        }

        /// <summary>
        /// 鷻加效果到效果列表
        /// </summary>
        private void CreateEffectItem(EffectData effect)
        {
            if (_effectItemPrefab == null || _effectsContainer == null) return;

            var itemObj = Instantiate(_effectItemPrefab, _effectsContainer);
            var textMesh = itemObj.GetComponent<TextMeshProUGUI>();
            if (textMesh != null)
            {
                textMesh.text = effect.Description ?? effect.Abbreviation;
            }
        }

        /// <summary>
        /// 鷻加效果到列表
        /// </summary>
        private void AddEffectToList(EffectDefinitionData effect)
        {
            if (effect == null) return;

            // 转换为EffectData
            var effectData = new EffectData
            {
                Abbreviation = effect.Id,
                Initiative = effect.ActivationType == (int)EffectActivationType.Voluntary,
                Parameters = effect.BaseSpeed,
                Speed = (EffectSpeed)effect.ActivationType,
                ManaType = ManaType.灰色,
                Description = effect.Description ?? effect.DisplayName
            };

            _currentCardData.Effects.Add(effectData);
            CreateEffectItem(effectData);
            ValidateParameters();
        }

        /// <summary>
        /// 打开效果组装器
        /// </summary>
        private void OnOpenEffectBuilderClicked()
        {
            // 通过UIManager打开效果组装器
            var effectBuilder = UIManager.Instance.GetPanel("EffectBuilder")?.GetComponent<EffectBuilderUI>();
            if (effectBuilder != null)
            {
                effectBuilder.SetEffect(null, AddEffectToList);
                effectBuilder.Show();
            }
            else
            {
                UIManager.Instance.ShowNotification("效果组装器未配置");
            }
        }

        /// <summary>
        /// 添加效果按钮点击（从效果列表添加）
        /// </summary>
        private void OnAddEffectClicked()
        {
            // 打开效果选择面板
            // 这里可以通过事件回调处理
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        private void ValidateParameters()
        {
            if (_currentCardData == null || _currentConfig == null)
            {
                SetValidationState(false, "参数无效");
                return;
            }

            // 检查必填参数
            bool allRequiredFilled = true;
            var missingParams = new List<string>();

            foreach (var required in _currentConfig.RequiredParameters)
            {
                if (!_requiredParameterValues.TryGetValue(required.ParameterType, out var value) || string.IsNullOrEmpty(value))
                {
                    allRequiredFilled = false;
                    missingParams.Add(required.DisplayName);
                }
            }

            // 更新UI
            if (allRequiredFilled)
            {
                SetValidationState(true, "参数验证通过");
                _confirmButton.interactable = true;
            }
            else
            {
                string message = $"请填写: {string.Join(", ", missingParams)}";
                SetValidationState(false, message);
                _confirmButton.interactable = false;
            }
        }

        /// <summary>
        /// 设置验证状态
        /// </summary>
        private void SetValidationState(bool isValid, string message)
        {
            if (_validationMessage != null)
            {
                _validationMessage.text = message;
                _validationMessage.color = isValid ? Color.green : Color.red;
            }

            if (_validationIcon != null)
            {
                _validationIcon.color = isValid ? Color.green : Color.red;
            }
        }

        /// <summary>
        /// 緻加效果按钮点击
        /// </summary>
        private void OnConfirmClicked()
        {
            if (_currentCardData == null) return;

            // 生成ID（如果没有名称，使用哈希值）
            if (string.IsNullOrEmpty(_currentCardData.CardName))
            {
                _currentCardData.CardName = _currentCardData.CalculateID();
            }

            // 触发确认回调
            OnCardConfirmed?.Invoke(_currentCardData);
        }

        /// <summary>
        /// 清除效果列表
        /// </summary>
        private void ClearEffectsList()
        {
            if (_effectsContainer == null) return;

            foreach (Transform child in _effectsContainer)
            {
                if (child != null && child.gameObject != _effectItemPrefab)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        #endregion

    }
}
