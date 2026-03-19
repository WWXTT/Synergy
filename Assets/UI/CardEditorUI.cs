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
    /// 卡牌编辑器 UI - 用于创建和编辑卡牌
    /// </summary>
    public class CardEditorUI : BaseUI
    {
        [Header("卡牌基本信息输入")]
        [SerializeField] private TMP_InputField _cardNameInput;
        [SerializeField] private TMP_Dropdown _cardTypeDropdown;
        [SerializeField] private TMP_InputField _illustrationInput;
        [SerializeField] private Toggle _legendaryToggle;
        [SerializeField] private Image _illustrationPreview;

        [Header("战斗属性输入")]
        [SerializeField] private GameObject _combatStatsPanel;
        [SerializeField] private TMP_InputField _lifeInput;
        [SerializeField] private TMP_InputField _powerInput;
        [SerializeField] private Toggle _hasLifeToggle;
        [SerializeField] private Toggle _hasPowerToggle;

        [Header("法力消耗输入")]
        [SerializeField] private GameObject _manaCostPanel;
        [SerializeField] private TMP_InputField[] _manaCostInputs; // 灰,红,蓝,绿,白,黑

        [Header("效果编辑")]
        [SerializeField] private GameObject _effectsPanel;
        [SerializeField] private Transform _effectsContainer;
        [SerializeField] private GameObject _effectItemPrefab;
        [SerializeField] private Button _addEffectButton;
        [SerializeField] private Button _openEffectBuilderButton;

        [Header("效果组装器引用")]
        [SerializeField] private EffectBuilderUI _effectBuilderUI;

        [Header("预览")]
        [SerializeField] private CardPreviewUI _cardPreview;
        [SerializeField] private TextMeshProUGUI _validationMessage;

        [Header("按钮")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _cancelButton;

        // 当前编辑的卡牌数据
        private CardData _currentCardData;
        private List<EffectData> _currentEffects = new List<EffectData>();
        private List<EffectEditorItemUI> _effectEditorItems = new List<EffectEditorItemUI>();

        // 高级效果定义列表（用于EffectBuilderUI）
        private List<EffectDefinitionData> _advancedEffects = new List<EffectDefinitionData>();

        // 法力类型枚举索引映射
        private static readonly ManaType[] ManaTypeOrder = new ManaType[]
        {
            ManaType.灰色, ManaType.红色, ManaType.蓝色, ManaType.绿色, ManaType.白色, ManaType.黑色
        };

        protected override void Initialize()
        {
            base.Initialize();

            // 初始化卡牌类型下拉菜单
            InitializeCardTypeDropdown();

            // 绑定按钮事件
            _saveButton?.AddClickListener(OnSaveClicked);
            _resetButton?.AddClickListener(OnResetClicked);
            _cancelButton?.AddClickListener(OnCancelClicked);
            _addEffectButton?.AddClickListener(OnAddEffectClicked);
            _openEffectBuilderButton?.AddClickListener(OnOpenEffectBuilderClicked);

            // 绑定输入事件（实时更新预览）
            _cardNameInput?.onValueChanged.AddListener(OnInputChanged);
            _cardTypeDropdown?.onValueChanged.AddListener(OnInputChanged);
            _illustrationInput?.onValueChanged.AddListener(OnInputChanged);
            _legendaryToggle?.onValueChanged.AddListener(OnInputChanged);

            // 绑定战斗属性输入事件
            _lifeInput?.onValueChanged.AddListener(OnInputChanged);
            _powerInput?.onValueChanged.AddListener(OnInputChanged);
            _hasLifeToggle?.onValueChanged.AddListener(OnHasLifeToggled);
            _hasPowerToggle?.onValueChanged.AddListener(OnHasPowerToggled);

            // 绑定法力消耗输入事件
            InitializeManaCostInputs();

            // 初始化法力消耗输入数组
            if (_manaCostInputs == null || _manaCostInputs.Length < 6)
            {
                _manaCostInputs = new TMP_InputField[6];
            }

            // 创建新卡牌数据
            CreateNewCard();

            // 隐藏战斗属性面板（默认）
            if (_combatStatsPanel != null)
            {
                _combatStatsPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 初始化卡牌类型下拉菜单
        /// </summary>
        private void InitializeCardTypeDropdown()
        {
            if (_cardTypeDropdown == null) return;

            _cardTypeDropdown.ClearOptions();

            List<string> options = new List<string>();
            foreach (CardType cardType in Enum.GetValues(typeof(CardType)))
            {
                options.Add(cardType.ToString());
            }

            _cardTypeDropdown.AddOptions(options);
            _cardTypeDropdown.value = 0;
        }

        /// <summary>
        /// 初始化法力消耗输入
        /// </summary>
        private void InitializeManaCostInputs()
        {
            if (_manaCostInputs == null) return;

            for (int i = 0; i < _manaCostInputs.Length; i++)
            {
                int index = i;
                _manaCostInputs[i]?.onValueChanged.AddListener((value) => OnManaCostChanged(index, value));
            }
        }

        /// <summary>
        /// 创建新卡牌数据
        /// </summary>
        private void CreateNewCard()
        {
            _currentCardData = new CardData
            {
                CardType = CardType.生物,
                CardName = "新卡牌",
                Illustration = string.Empty,
                Life = null,
                Power = null,
                Cost = new Dictionary<int, float>(),
                Effects = new List<EffectData>(),
                IsLegendary = false,
                CreationTime = DateTime.Now
            };

            _currentEffects.Clear();
            RefreshEffectsList();
            UpdateUIFromCardData();
        }

        /// <summary>
        /// 从 UI 数据更新到卡牌数据
        /// </summary>
        private void UpdateCardDataFromUI()
        {
            if (_currentCardData == null) return;

            // 基本信息
            _currentCardData.CardName = _cardNameInput?.text ?? string.Empty;
            _currentCardData.CardType = (CardType)_cardTypeDropdown.value;
            _currentCardData.Illustration = _illustrationInput?.text ?? string.Empty;
            _currentCardData.IsLegendary = _legendaryToggle?.isOn ?? false;

            // 战斗属性
            if (_currentCardData.CardType == CardType.生物 || _currentCardData.CardType == CardType.传奇)
            {
                if (_hasLifeToggle?.isOn ?? false)
                {
                    _currentCardData.Life = int.TryParse(_lifeInput?.text, out int life) ? life : (int?)null;
                }
                else
                {
                    _currentCardData.Life = null;
                }

                if (_hasPowerToggle?.isOn ?? false)
                {
                    _currentCardData.Power = int.TryParse(_powerInput?.text, out int power) ? power : (int?)null;
                }
                else
                {
                    _currentCardData.Power = null;
                }
            }
            else
            {
                _currentCardData.Life = null;
                _currentCardData.Power = null;
            }

            // 法力消耗
            _currentCardData.Cost.Clear();
            if (_manaCostInputs != null)
            {
                for (int i = 0; i < _manaCostInputs.Length && i < ManaTypeOrder.Length; i++)
                {
                    if (_manaCostInputs[i] != null && float.TryParse(_manaCostInputs[i].text, out float cost))
                    {
                        if (cost > 0)
                        {
                            _currentCardData.Cost[(int)ManaTypeOrder[i]] = cost;
                        }
                    }
                }
            }

            // 效果列表
            _currentCardData.Effects = new List<EffectData>(_currentEffects);

            // 重置缓存
            _currentCardData.ResetCache();
        }

        /// <summary>
        /// 从卡牌数据更新 UI
        /// </summary>
        private void UpdateUIFromCardData()
        {
            if (_currentCardData == null) return;

            // 基本信息
            _cardNameInput.text = _currentCardData.CardName ?? string.Empty;
            _cardTypeDropdown.value = (int)_currentCardData.CardType;
            _illustrationInput.text = _currentCardData.Illustration ?? string.Empty;
            _legendaryToggle.isOn = _currentCardData.IsLegendary;

            // 战斗属性
            bool showCombatStats = _currentCardData.CardType == CardType.生物 || _currentCardData.CardType == CardType.传奇;
            if (_combatStatsPanel != null)
            {
                _combatStatsPanel.SetActive(showCombatStats);
            }

            if (showCombatStats)
            {
                bool hasLife = _currentCardData.Life.HasValue;
                bool hasPower = _currentCardData.Power.HasValue;

                _hasLifeToggle.isOn = hasLife;
                _hasPowerToggle.isOn = hasPower;

                _lifeInput.text = hasLife ? _currentCardData.Life.Value.ToString() : string.Empty;
                _powerInput.text = hasPower ? _currentCardData.Power.Value.ToString() : string.Empty;
            }

            // 法力消耗
            UpdateManaCostInputsFromCardData();

            // 更新预览
            UpdatePreview();

            // 验证卡牌
            ValidateCard();
        }

        /// <summary>
        /// 从卡牌数据更新法力消耗输入
        /// </summary>
        private void UpdateManaCostInputsFromCardData()
        {
            if (_manaCostInputs == null || _currentCardData == null) return;

            for (int i = 0; i < _manaCostInputs.Length && i < ManaTypeOrder.Length; i++)
            {
                if (_manaCostInputs[i] != null)
                {
                    int manaTypeInt = (int)ManaTypeOrder[i];
                    if (_currentCardData.Cost.TryGetValue(manaTypeInt, out float cost))
                    {
                        _manaCostInputs[i].text = cost > 0 ? cost.ToString() : string.Empty;
                    }
                    else
                    {
                        _manaCostInputs[i].text = string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// 更新预览
        /// </summary>
        private void UpdatePreview()
        {
            UpdateCardDataFromUI();

            if (_cardPreview != null)
            {
                _cardPreview.SetCardData(_currentCardData);
            }

            // 更新立绘预览
            if (_illustrationPreview != null)
            {
                // TODO: 从路径加载立绘图片
            }
        }

        /// <summary>
        /// 验证卡牌
        /// </summary>
        private void ValidateCard()
        {
            if (_validationMessage == null) return;

            if (_currentCardData.Validate(out string error))
            {
                _validationMessage.text = "验证通过";
                _validationMessage.color = Color.green;
                _saveButton.interactable = true;
            }
            else
            {
                _validationMessage.text = error ?? "验证失败";
                _validationMessage.color = Color.red;
                _saveButton.interactable = false;
            }
        }

        /// <summary>
        /// 刷新效果列表
        /// </summary>
        private void RefreshEffectsList()
        {
            // 清除现有的效果编辑器项
            foreach (var item in _effectEditorItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
            }
            _effectEditorItems.Clear();

            // 创建新的效果编辑器项
            foreach (var effect in _currentEffects)
            {
                CreateEffectEditorItem(effect);
            }
        }

        /// <summary>
        /// 创建效果编辑器项
        /// </summary>
        private void CreateEffectEditorItem(EffectData effect)
        {
            if (_effectItemPrefab == null || _effectsContainer == null) return;

            GameObject itemObj = Instantiate(_effectItemPrefab, _effectsContainer);
            var itemUI = itemObj.GetComponent<EffectEditorItemUI>();
            if (itemUI != null)
            {
                itemUI.Initialize(effect, OnEffectChanged, OnEffectRemoved);
                _effectEditorItems.Add(itemUI);
            }
        }

        // ==================== 事件处理 ====================

        /// <summary>
        /// 输入改变时调用
        /// </summary>
        private void OnInputChanged(string value)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 输入改变时调用（下拉菜单）
        /// </summary>
        private void OnInputChanged(int value)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 输入改变时调用（Toggle）
        /// </summary>
        private void OnInputChanged(bool value)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 法力消耗改变时调用
        /// </summary>
        private void OnManaCostChanged(int index, string value)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 是否有生命值切换
        /// </summary>
        private void OnHasLifeToggled(bool isOn)
        {
            if (_lifeInput != null)
            {
                _lifeInput.gameObject.SetActive(isOn);
            }
            UpdatePreview();
        }

        /// <summary>
        /// 是否有攻击力切换
        /// </summary>
        private void OnHasPowerToggled(bool isOn)
        {
            if (_powerInput != null)
            {
                _powerInput.gameObject.SetActive(isOn);
            }
            UpdatePreview();
        }

        /// <summary>
        /// 添加效果按钮点击
        /// </summary>
        private void OnAddEffectClicked()
        {
            var newEffect = new EffectData
            {
                Abbreviation = string.Empty,
                Initiative = true,
                Parameters = 0,
                Speed = EffectSpeed.可选诱发,
                ManaType = ManaType.灰色,
                Description = string.Empty
            };
            _currentEffects.Add(newEffect);
            CreateEffectEditorItem(newEffect);
        }

        /// <summary>
        /// 效果改变时调用
        /// </summary>
        private void OnEffectChanged(EffectData effect)
        {
            UpdatePreview();
        }

        /// <summary>
        /// 效果移除时调用
        /// </summary>
        private void OnEffectRemoved(EffectData effect)
        {
            _currentEffects.Remove(effect);
            _effectEditorItems.RemoveAll(item => item.Effect == effect);
            UpdatePreview();
        }

        /// <summary>
        /// 打开效果组装器按钮点击
        /// </summary>
        private void OnOpenEffectBuilderClicked()
        {
            if (_effectBuilderUI != null)
            {
                _effectBuilderUI.SetEffect(null, OnEffectBuilderConfirmed);
                _effectBuilderUI.Show();
            }
            else
            {
                UIManager.Instance.ShowNotification("效果组装器未配置");
            }
        }

        /// <summary>
        /// 效果组装器确认回调
        /// </summary>
        private void OnEffectBuilderConfirmed(EffectDefinitionData effectData)
        {
            if (effectData == null) return;

            // 添加到高级效果列表
            _advancedEffects.Add(effectData);

            // 同时创建一个简化的 EffectData 用于预览
            var simpleEffect = new EffectData
            {
                Abbreviation = effectData.Id,
                Initiative = effectData.ActivationType == (int)EffectActivationType.Voluntary,
                Parameters = effectData.BaseSpeed,
                Speed = (EffectSpeed)effectData.ActivationType,
                ManaType = ManaType.灰色,
                Description = effectData.Description ?? effectData.DisplayName
            };

            _currentEffects.Add(simpleEffect);
            CreateEffectEditorItem(simpleEffect);

            UpdatePreview();

            UIManager.Instance.ShowNotification($"已添加效果: {effectData.DisplayName}");
        }

        /// <summary>
        /// 保存按钮点击
        /// </summary>
        private void OnSaveClicked()
        {
            UpdateCardDataFromUI();

            // 验证卡牌
            if (!_currentCardData.Validate(out string error))
            {
                UIManager.Instance.ShowNotification($"保存失败: {error}");
                return;
            }

            // 保存到注册表
            SaveCardAsync().Forget();
        }

        /// <summary>
        /// 保存卡牌异步
        /// </summary>
        private async UniTaskVoid SaveCardAsync()
        {
            UIManager.Instance.ShowLoading(true);

            bool success = false;
            string message = string.Empty;

            // 尝试注册新卡牌
            if (CardDataRegistry.Instance.RegisterCard(_currentCardData, out string errorMessage))
            {
                // 异步保存
                await CardDataRegistry.Instance.SaveAsync();
                success = true;
                message = $"卡牌 '{_currentCardData.CardName}' 保存成功！";
            }
            else
            {
                success = false;
                message = $"保存失败: {errorMessage}";
            }

            UIManager.Instance.ShowLoading(false);
            UIManager.Instance.ShowNotification(message);

            if (success)
            {
                // 可选：跳转到卡牌列表
                // UIManager.Instance.ShowPanel("CardList");
            }
        }

        /// <summary>
        /// 重置按钮点击
        /// </summary>
        private void OnResetClicked()
        {
            CreateNewCard();
            UIManager.Instance.ShowNotification("已重置卡牌数据");
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void OnCancelClicked()
        {
            Hide();
            // 返回到上一个界面
            // TODO: 实现返回逻辑
        }

        /// <summary>
        /// 设置编辑的卡牌（用于编辑现有卡牌）
        /// </summary>
        public void SetCard(CardData cardData)
        {
            if (cardData == null) return;

            // 深拷贝卡牌数据
            string json = JsonUtility.ToJson(cardData);
            _currentCardData = JsonUtility.FromJson<CardData>(json);
            _currentCardData.OnAfterDeserialize();

            _currentEffects = new List<EffectData>(_currentCardData.Effects);
            RefreshEffectsList();
            UpdateUIFromCardData();
        }

        protected override void OnShow()
        {
            base.OnShow();
            // 确保注册表已加载
            if (!CardDataRegistry.Instance.IsLoaded)
            {
                LoadRegistryAndRefreshAsync().Forget();
            }
        }

        /// <summary>
        /// 加载注册表并刷新
        /// </summary>
        private async UniTaskVoid LoadRegistryAndRefreshAsync()
        {
            UIManager.Instance.ShowLoading(true);
            await CardDataRegistry.Instance.LoadAsync();
            UIManager.Instance.ShowLoading(false);
            UpdatePreview();
        }
    }
}
