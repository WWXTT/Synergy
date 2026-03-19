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
    /// 卡牌列表 UI - 用于展示卡牌库中的所有卡牌
    /// </summary>
    public class CardListUI : BaseUI
    {
        [Header("卡牌列表")]
        [SerializeField] private Transform _cardContainer;
        [SerializeField] private GameObject _cardItemPrefab;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("搜索和过滤")]
        [SerializeField] private TMP_InputField _searchInput;
        [SerializeField] private TMP_Dropdown _cardTypeFilter;
        [SerializeField] private TMP_Dropdown _manaTypeFilter;
        [SerializeField] private Slider _minCostSlider;
        [SerializeField] private Slider _maxCostSlider;
        [SerializeField] private TMP_Text _minCostText;
        [SerializeField] private TMP_Text _maxCostText;
        [SerializeField] private Button _filterButton;
        [SerializeField] private Button _resetFilterButton;

        [Header("统计信息")]
        [SerializeField] private TMP_Text _totalCardsText;
        [SerializeField] private TMP_Text _filteredCardsText;

        [Header("按钮")]
        [SerializeField] private Button _createNewCardButton;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _deleteAllButton;

        // 卡牌列表项
        private List<CardListItemUI> _cardItems = new List<CardListItemUI>();
        private List<CardData> _filteredCards = new List<CardData>();

        // 选中的卡牌
        private CardListItemUI _selectedCardItem;

        public CardListItemUI SelectedCardItem => _selectedCardItem;
        public CardData SelectedCard => _selectedCardItem?.CardData;

        // 卡牌选中事件
        public event Action<CardData> OnCardSelected;
        public event Action<CardData> OnCardEditRequested;
        public event Action<CardData> OnCardDeleteRequested;

        protected override void Initialize()
        {
            base.Initialize();

            // 初始化下拉菜单
            InitializeCardTypeFilter();
            InitializeManaTypeFilter();

            // 初始化滑动条
            InitializeCostSliders();

            // 绑定按钮事件
            _createNewCardButton?.AddClickListener(OnCreateNewCardClicked);
            _refreshButton?.AddClickListener(OnRefreshClicked);
            _deleteAllButton?.AddClickListener(OnDeleteAllClicked);
            _filterButton?.AddClickListener(OnFilterButtonClicked);
            _resetFilterButton?.AddClickListener(OnResetFilterButtonClicked);

            // 绑定搜索输入事件
            if (_searchInput != null)
            {
                _searchInput.onValueChanged.AddListener(OnSearchChanged);
                _searchInput.onEndEdit.AddListener(OnSearchSubmitted);
            }

            // 绑定过滤下拉菜单事件
            _cardTypeFilter?.onValueChanged.AddListener(OnFilterChanged);
            _manaTypeFilter?.onValueChanged.AddListener(OnFilterChanged);

            // 绑定滑动条事件
            _minCostSlider?.onValueChanged.AddListener(OnCostSliderChanged);
            _maxCostSlider?.onValueChanged.AddListener(OnCostSliderChanged);

            // 清除选中
            _selectedCardItem = null;
        }

        /// <summary>
        /// 初始化卡牌类型过滤器
        /// </summary>
        private void InitializeCardTypeFilter()
        {
            if (_cardTypeFilter == null) return;

            _cardTypeFilter.ClearOptions();

            List<string> options = new List<string> { "全部" };
            foreach (CardType cardType in Enum.GetValues(typeof(CardType)))
            {
                options.Add(cardType.ToString());
            }

            _cardTypeFilter.AddOptions(options);
        }

        /// <summary>
        /// 初始化法力类型过滤器
        /// </summary>
        private void InitializeManaTypeFilter()
        {
            if (_manaTypeFilter == null) return;

            _manaTypeFilter.ClearOptions();

            List<string> options = new List<string> { "全部" };
            foreach (ManaType manaType in Enum.GetValues(typeof(ManaType)))
            {
                options.Add(manaType.ToString());
            }

            _manaTypeFilter.AddOptions(options);
        }

        /// <summary>
        /// 初始化费用滑动条
        /// </summary>
        private void InitializeCostSliders()
        {
            if (_minCostSlider != null)
            {
                _minCostSlider.minValue = 0;
                _minCostSlider.maxValue = 15;
                _minCostSlider.value = 0;
            }

            if (_maxCostSlider != null)
            {
                _maxCostSlider.minValue = 0;
                _maxCostSlider.maxValue = 15;
                _maxCostSlider.value = 15;
            }

            UpdateCostSliderText();
        }

        /// <summary>
        /// 刷新卡牌列表
        /// </summary>
        public void RefreshCardList()
        {
            RefreshCardListAsync().Forget();
        }

        /// <summary>
        /// 刷新卡牌列表异步
        /// </summary>
        private async UniTaskVoid RefreshCardListAsync()
        {
            UIManager.Instance.ShowLoading(true);

            // 确保注册表已加载
            if (!CardDataRegistry.Instance.IsLoaded)
            {
                await CardDataRegistry.Instance.LoadAsync();
            }

            // 过滤卡牌
            FilterCards();

            // 清除现有列表
            ClearCardList();

            // 创建卡牌列表项
            foreach (var cardData in _filteredCards)
            {
                CreateCardListItem(cardData);
            }

            // 更新统计信息
            UpdateStatistics();

            UIManager.Instance.ShowLoading(false);
        }

        /// <summary>
        /// 过滤卡牌
        /// </summary>
        private void FilterCards()
        {
            _filteredCards.Clear();

            string searchQuery = _searchInput?.text ?? string.Empty;
            string searchQueryLower = searchQuery.ToLower();

            int cardTypeFilterValue = _cardTypeFilter?.value ?? 0;
            int manaTypeFilterValue = _manaTypeFilter?.value ?? 0;
            float minCost = _minCostSlider?.value ?? 0;
            float maxCost = _maxCostSlider?.value ?? 15;

            foreach (var cardData in CardDataRegistry.Instance.AllCards)
            {
                // 搜索匹配
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    bool nameMatch = cardData.CardName?.ToLower().Contains(searchQueryLower) ?? false;
                    bool typeMatch = cardData.CardType.ToString().ToLower().Contains(searchQueryLower);
                    bool effectMatch = false;

                    foreach (var effect in cardData.Effects)
                    {
                        if ((effect.Abbreviation?.ToLower().Contains(searchQueryLower) ?? false) ||
                            (effect.Description?.ToLower().Contains(searchQueryLower) ?? false))
                        {
                            effectMatch = true;
                            break;
                        }
                    }

                    if (!nameMatch && !typeMatch && !effectMatch)
                    {
                        continue;
                    }
                }

                // 卡牌类型过滤
                if (cardTypeFilterValue > 0)
                {
                    CardType filterType = (CardType)(cardTypeFilterValue - 1);
                    if (cardData.CardType != filterType)
                    {
                        continue;
                    }
                }

                // 法力类型过滤
                if (manaTypeFilterValue > 0)
                {
                    ManaType filterMana = (ManaType)(manaTypeFilterValue - 1);
                    bool hasManaType = false;
                    foreach (var cost in cardData.Cost)
                    {
                        if ((ManaType)cost.Key == filterMana && cost.Value > 0)
                        {
                            hasManaType = true;
                            break;
                        }
                    }
                    if (!hasManaType)
                    {
                        continue;
                    }
                }

                // 费用范围过滤
                if (cardData.TotalCost < minCost || cardData.TotalCost > maxCost)
                {
                    continue;
                }

                _filteredCards.Add(cardData);
            }
        }

        /// <summary>
        /// 创建卡牌列表项
        /// </summary>
        private void CreateCardListItem(CardData cardData)
        {
            if (_cardItemPrefab == null || _cardContainer == null) return;

            GameObject itemObj = Instantiate(_cardItemPrefab, _cardContainer);
            var itemUI = itemObj.GetComponent<CardListItemUI>();

            if (itemUI != null)
            {
                itemUI.Initialize(cardData, this);
                _cardItems.Add(itemUI);
            }
        }

        /// <summary>
        /// 清除卡牌列表
        /// </summary>
        private void ClearCardList()
        {
            foreach (var item in _cardItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
            }
            _cardItems.Clear();
            _selectedCardItem = null;
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            if (_totalCardsText != null)
            {
                _totalCardsText.text = $"总卡牌数: {CardDataRegistry.Instance.Count}";
            }

            if (_filteredCardsText != null)
            {
                _filteredCardsText.text = $"显示: {_filteredCards.Count}";
            }
        }

        /// <summary>
        /// 更新费用滑动条文本
        /// </summary>
        private void UpdateCostSliderText()
        {
            if (_minCostText != null && _minCostSlider != null)
            {
                _minCostText.text = _minCostSlider.value.ToString("F1");
            }

            if (_maxCostText != null && _maxCostSlider != null)
            {
                _maxCostText.text = _maxCostSlider.value.ToString("F1");
            }
        }

        // ==================== 事件处理 ====================

        /// <summary>
        /// 搜索输入改变时调用
        /// </summary>
        private void OnSearchChanged(string value)
        {
            // 实时搜索（可选，如果卡牌数量较少）
            // FilterCards();
            // RefreshCardList();
        }

        /// <summary>
        /// 搜索提交时调用
        /// </summary>
        private void OnSearchSubmitted(string value)
        {
            FilterCards();
            RefreshCardList();
        }

        /// <summary>
        /// 过滤器改变时调用
        /// </summary>
        private void OnFilterChanged(int value)
        {
            // 不自动刷新，等待点击过滤按钮
        }

        /// <summary>
        /// 费用滑动条改变时调用
        /// </summary>
        private void OnCostSliderChanged(float value)
        {
            UpdateCostSliderText();

            // 确保最小值不超过最大值
            if (_minCostSlider != null && _maxCostSlider != null)
            {
                if (_minCostSlider.value > _maxCostSlider.value)
                {
                    if (_minCostSlider == _minCostSlider.GetComponent<Slider>())
                    {
                        _minCostSlider.value = _maxCostSlider.value;
                    }
                    else
                    {
                        _maxCostSlider.value = _minCostSlider.value;
                    }
                }
            }
        }

        /// <summary>
        /// 过滤按钮点击
        /// </summary>
        private void OnFilterButtonClicked()
        {
            FilterCards();
            RefreshCardList();
        }

        /// <summary>
        /// 重置过滤按钮点击
        /// </summary>
        private void OnResetFilterButtonClicked()
        {
            // 重置过滤器
            if (_searchInput != null)
            {
                _searchInput.text = string.Empty;
            }

            if (_cardTypeFilter != null)
            {
                _cardTypeFilter.value = 0;
            }

            if (_manaTypeFilter != null)
            {
                _manaTypeFilter.value = 0;
            }

            if (_minCostSlider != null)
            {
                _minCostSlider.value = 0;
            }

            if (_maxCostSlider != null)
            {
                _maxCostSlider.value = 15;
            }

            UpdateCostSliderText();
            FilterCards();
            RefreshCardList();
        }

        /// <summary>
        /// 创建新卡牌按钮点击
        /// </summary>
        private void OnCreateNewCardClicked()
        {
            // 切换到卡牌编辑器，创建新卡牌
            var cardEditor = FindObjectOfType<CardEditorUI>();
            if (cardEditor != null)
            {
                cardEditor.Show();
            }
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void OnRefreshClicked()
        {
            RefreshCardList();
            UIManager.Instance.ShowNotification("卡牌列表已刷新");
        }

        /// <summary>
        /// 删除所有按钮点击
        /// </summary>
        private void OnDeleteAllClicked()
        {
            // TODO: 显示确认对话框
            DeleteAllCards();
        }

        /// <summary>
        /// 删除所有卡牌
        /// </summary>
        private void DeleteAllCards()
        {
            DeleteAllCardsAsync().Forget();
        }

        /// <summary>
        /// 删除所有卡牌异步
        /// </summary>
        private async UniTaskVoid DeleteAllCardsAsync()
        {
            UIManager.Instance.ShowLoading(true);

            // 清空注册表
            CardDataRegistry.Instance.Clear();

            // 保存
            await CardDataRegistry.Instance.SaveAsync();

            // 刷新列表
            RefreshCardList();

            UIManager.Instance.ShowLoading(false);
            UIManager.Instance.ShowNotification("所有卡牌已删除");
        }

        /// <summary>
        /// 设置选中的卡牌
        /// </summary>
        public void SetSelectedCard(CardListItemUI cardItem)
        {
            // 取消之前的选择
            if (_selectedCardItem != null)
            {
                _selectedCardItem.SetSelected(false);
            }

            // 设置新选择
            _selectedCardItem = cardItem;
            if (_selectedCardItem != null)
            {
                _selectedCardItem.SetSelected(true);
            }

            // 触发事件
            OnCardSelected?.Invoke(_selectedCardItem?.CardData);
        }

        /// <summary>
        /// 编辑卡牌
        /// </summary>
        public void EditCard(CardData cardData)
        {
            OnCardEditRequested?.Invoke(cardData);
        }

        /// <summary>
        /// 删除卡牌
        /// </summary>
        public void DeleteCard(CardData cardData)
        {
            OnCardDeleteRequested?.Invoke(cardData);
        }

        protected override void OnShow()
        {
            base.OnShow();
            RefreshCardList();
        }
    }

    /// <summary>
    /// 卡牌列表项 UI
    /// </summary>
    public class CardListItemUI : MonoBehaviour
    {
        [Header("卡牌信息显示")]
        [SerializeField] private Image _cardBackground;
        [SerializeField] private TextMeshProUGUI _cardNameText;
        [SerializeField] private TextMeshProUGUI _cardTypeText;
        [SerializeField] private TextMeshProUGUI _cardCostText;
        [SerializeField] private GameObject _combatStatsPanel;
        [SerializeField] private TextMeshProUGUI _lifeText;
        [SerializeField] private TextMeshProUGUI _powerText;
        [SerializeField] private GameObject _selectedIndicator;

        [Header("按钮")]
        [SerializeField] private Button _editButton;
        [SerializeField] private Button _deleteButton;

        [Header("颜色配置")]
        [SerializeField] private Color _selectedColor = new Color(0.5f, 0.8f, 1f);
        [SerializeField] private Color _normalColor = new Color(0.9f, 0.9f, 0.9f);

        private CardData _cardData;
        private CardListUI _parentList;

        public CardData CardData => _cardData;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(CardData cardData, CardListUI parentList)
        {
            _cardData = cardData;
            _parentList = parentList;

            // 绑定按钮事件
            _editButton?.AddClickListener(OnEditClicked);
            _deleteButton?.AddClickListener(OnDeleteClicked);

            // 绑定点击事件
            Button backgroundButton = GetComponent<Button>();
            if (backgroundButton == null)
            {
                backgroundButton = gameObject.AddComponent<Button>();
                var image = GetComponent<Image>();
                if (image != null)
                {
                    image.raycastTarget = true;
                }
            }
            backgroundButton.AddClickListener(OnItemClicked);

            UpdateUI();
        }

        /// <summary>
        /// 更新 UI
        /// </summary>
        private void UpdateUI()
        {
            if (_cardData == null) return;

            // 卡牌名称
            if (_cardNameText != null)
            {
                _cardNameText.text = _cardData.CardName ?? string.Empty;
            }

            // 卡牌类型
            if (_cardTypeText != null)
            {
                _cardTypeText.text = _cardData.CardType.ToString();
            }

            // 卡牌费用
            if (_cardCostText != null)
            {
                _cardCostText.text = _cardData.TotalCost.ToString("F1");
            }

            // 战斗属性
            bool showStats = _cardData.CardType == CardType.生物 ||
                             _cardData.CardType == CardType.传奇;
            if (_combatStatsPanel != null)
            {
                _combatStatsPanel.SetActive(showStats);
            }

            if (showStats)
            {
                if (_lifeText != null)
                {
                    _lifeText.text = _cardData.Life.HasValue ? _cardData.Life.Value.ToString() : "-";
                }
                if (_powerText != null)
                {
                    _powerText.text = _cardData.Power.HasValue ? _cardData.Power.Value.ToString() : "-";
                }
            }

            // 卡牌边框颜色
            if (_cardBackground != null)
            {
                _cardBackground.color = GetCardTypeColor(_cardData.CardType);
            }
        }

        /// <summary>
        /// 获取卡牌类型颜色
        /// </summary>
        private Color GetCardTypeColor(CardType cardType)
        {
            switch (cardType)
            {
                case CardType.生物:
                    return new Color(0.8f, 0.8f, 0.6f);
                case CardType.传奇:
                    return new Color(1f, 0.84f, 0f);
                case CardType.术法:
                    return new Color(0.6f, 0.6f, 0.9f);
                case CardType.领域:
                    return new Color(0.6f, 0.9f, 0.6f);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 设置选中状态
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_selectedIndicator != null)
            {
                _selectedIndicator.SetActive(selected);
            }

            if (_cardBackground != null)
            {
                _cardBackground.color = selected ? _selectedColor : GetCardTypeColor(_cardData.CardType);
            }
        }

        /// <summary>
        /// 列表项点击
        /// </summary>
        private void OnItemClicked()
        {
            _parentList?.SetSelectedCard(this);
        }

        /// <summary>
        /// 编辑按钮点击
        /// </summary>
        private void OnEditClicked()
        {
            _parentList?.EditCard(_cardData);
        }

        /// <summary>
        /// 删除按钮点击
        /// </summary>
        private void OnDeleteClicked()
        {
            // TODO: 显示确认对话框
            _parentList?.DeleteCard(_cardData);
        }
    }
}
