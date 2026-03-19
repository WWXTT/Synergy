using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using CardCore.Data;
using cfg;

namespace CardCore.UI.UIToolkit
{
    /// <summary>
    /// 卡组编辑器面板
    /// 使用 UI Toolkit 实现的三栏布局卡组编辑界面
    /// 支持拖拽式编辑
    /// </summary>
    public class DeckEditorPanel : BaseUIToolkitPanel
    {
        [Header("数据引用")]
        [SerializeField] private TempCardDataSO _cardDataSO;
        [SerializeField] private TempDeckDataSO _deckDataSO;

        // 可用卡牌
        private TextField _searchInput;
        private VisualElement _filterTags;
        private ScrollView _availableCards;

        // 卡组区域
        private Label _deckNameDisplay;
        private VisualElement _mainDeckArea;
        private ScrollView _mainDeckCards;
        private Label _mainDeckCount;
        private VisualElement _extraDeckArea;
        private ScrollView _extraDeckCards;
        private Label _extraDeckCount;

        // 卡组信息
        private TextField _deckNameInput;
        private TextField _deckDescInput;
        private Label _statTotal;
        private Label _statCreatures;
        private Label _statSpells;
        private Label _statDomains;
        private Label _statLegendaries;
        private VisualElement _colorDistribution;

        // 按钮
        private Button _btnSaveDeck;
        private Button _btnExportDeck;
        private Button _btnDeleteDeck;

        // 数据
        private List<TempCardData> _availableCardList = new List<TempCardData>();
        private TempDeckData _currentDeck;
        private Dictionary<string, int> _mainDeckCardCounts = new Dictionary<string, int>();
        private Dictionary<string, int> _extraDeckCardCounts = new Dictionary<string, int>();
        private bool _isDirty;

        // 筛选状态
        private string _currentFilter = "全部";

        protected override void Awake()
        {
            base.Awake();
            _panelName = "DeckEditor";
        }

        protected override void BindUIElements()
        {
            base.BindUIElements();

            // 可用卡牌
            _searchInput = Q<TextField>("search-input");
            _filterTags = Q("filter-tags");
            _availableCards = Q<ScrollView>("available-cards");

            // 卡组区域
            _deckNameDisplay = Q<Label>("deck-name-display");
            _mainDeckArea = Q("main-deck-area");
            _mainDeckCards = Q<ScrollView>("main-deck-cards");
            _mainDeckCount = Q<Label>("main-deck-count");
            _extraDeckArea = Q("extra-deck-area");
            _extraDeckCards = Q<ScrollView>("extra-deck-cards");
            _extraDeckCount = Q<Label>("extra-deck-count");

            // 卡组信息
            _deckNameInput = Q<TextField>("deck-name-input");
            _deckDescInput = Q<TextField>("deck-desc-input");
            _statTotal = Q<Label>("stat-total");
            _statCreatures = Q<Label>("stat-creatures");
            _statSpells = Q<Label>("stat-spells");
            _statDomains = Q<Label>("stat-domains");
            _statLegendaries = Q<Label>("stat-legendaries");
            _colorDistribution = Q("color-distribution");

            // 按钮
            _btnSaveDeck = Q<Button>("btn-save-deck");
            _btnExportDeck = Q<Button>("btn-export-deck");
            _btnDeleteDeck = Q<Button>("btn-delete-deck");
        }

        protected override void RegisterEvents()
        {
            base.RegisterEvents();

            // 搜索
            _searchInput?.RegisterValueChangedCallback(OnSearchChanged);

            // 筛选标签
            var filterButtons = _filterTags?.Query<Button>().ToList();
            if (filterButtons != null)
            {
                foreach (var btn in filterButtons)
                {
                    btn.RegisterCallback<ClickEvent>(OnFilterClick);
                }
            }

            // 卡组信息编辑
            _deckNameInput?.RegisterValueChangedCallback(OnDeckNameChanged);
            _deckDescInput?.RegisterValueChangedCallback(OnDeckDescChanged);

            // 拖拽目标区域
            SetupDragAndDrop();

            // 按钮
            _btnSaveDeck?.RegisterCallback<ClickEvent>(OnSaveDeckClick);
            _btnExportDeck?.RegisterCallback<ClickEvent>(OnExportDeckClick);
            _btnDeleteDeck?.RegisterCallback<ClickEvent>(OnDeleteDeckClick);
        }

        private void SetupDragAndDrop()
        {
            // 主卡组拖拽目标
            _mainDeckArea?.RegisterCallback<DragEnterEvent>(OnDragEnter);
            _mainDeckArea?.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            _mainDeckArea?.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            _mainDeckArea?.RegisterCallback<DragPerformEvent>(OnDragPerformMainDeck);

            // 额外卡组拖拽目标
            _extraDeckArea?.RegisterCallback<DragEnterEvent>(OnDragEnter);
            _extraDeckArea?.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            _extraDeckArea?.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            _extraDeckArea?.RegisterCallback<DragPerformEvent>(OnDragPerformExtraDeck);
        }

        public override void Refresh()
        {
            base.Refresh();
            LoadAvailableCards();
            LoadCurrentDeck();
        }

        private void LoadAvailableCards()
        {
            _availableCardList.Clear();

            if (_cardDataSO != null)
            {
                _availableCardList = new List<TempCardData>(_cardDataSO.cards);
            }

            RefreshAvailableCards();
        }

        private void RefreshAvailableCards()
        {
            if (_availableCards == null) return;

            _availableCards.Clear();

            var filteredCards = FilterCards(_availableCardList);

            foreach (var card in filteredCards)
            {
                var item = CreateAvailableCardItem(card);
                _availableCards.Add(item);
            }
        }

        private List<TempCardData> FilterCards(List<TempCardData> cards)
        {
            var filtered = cards;

            // 类型筛选
            if (_currentFilter != "全部")
            {
                filtered = filtered.Where(c =>
                {
                    return _currentFilter switch
                    {
                        "生物" => c.cardType == CardType.生物,
                        "术法" => c.cardType == CardType.术法,
                        "领域" => c.cardType == CardType.领域,
                        _ => true
                    };
                }).ToList();
            }

            // 搜索筛选
            var searchText = _searchInput?.value ?? "";
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(c =>
                    c.cardName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    c.description.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return filtered;
        }

        private VisualElement CreateAvailableCardItem(TempCardData card)
        {
            var item = new VisualElement { name = $"available-card-{card.id}" };
            item.AddToClassList("available-card-item");

            // 图标
            var icon = new VisualElement { name = "card-icon" };
            icon.AddToClassList("available-card-item-icon");
            icon.AddToClassList(GetCardTypeClass(card.cardType));
            item.Add(icon);

            // 信息
            var info = new VisualElement { name = "card-info" };
            info.AddToClassList("available-card-item-info");

            var name = new Label(card.cardName) { name = "card-name" };
            name.AddToClassList("available-card-item-name");
            info.Add(name);

            var type = new Label($"{card.cardType} · {card.GetTotalCost()}费") { name = "card-type" };
            type.AddToClassList("available-card-item-type");
            info.Add(type);

            item.Add(info);

            // 费用
            var cost = new Label(card.GetTotalCost().ToString()) { name = "card-cost" };
            cost.AddToClassList("available-card-item-cost");
            item.Add(cost);

            // 拖拽源
            item.RegisterCallback<MouseDownEvent>(evt => OnCardDragStart(evt, card));
            item.RegisterCallback<MouseUpEvent>(evt => OnCardDragEnd(evt));
            item.RegisterCallback<MouseMoveEvent>(evt => OnCardDragMove(evt));

            // 双击添加
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    AddCardToDeck(card);
                }
            });

            return item;
        }

        private string GetCardTypeClass(CardType type)
        {
            return type switch
            {
                CardType.生物 => "creature",
                CardType.传奇 => "legendary",
                CardType.术法 => "spell",
                CardType.领域 => "domain",
                _ => ""
            };
        }

        private void LoadCurrentDeck()
        {
            if (_deckDataSO != null && _deckDataSO.decks.Count > 0)
            {
                _currentDeck = _deckDataSO.decks[0]; // 默认加载第一个卡组
            }
            else
            {
                _currentDeck = new TempDeckData
                {
                    id = $"DECK_{DateTime.Now.Ticks}",
                    deckName = "新卡组",
                    description = ""
                };
            }

            // 重建卡牌计数
            RebuildDeckCounts();
            UpdateDeckDisplay();
            UpdateStatistics();
        }

        private void RebuildDeckCounts()
        {
            _mainDeckCardCounts.Clear();
            _extraDeckCardCounts.Clear();

            if (_currentDeck?.mainDeckCards != null)
            {
                foreach (var entry in _currentDeck.mainDeckCards)
                {
                    _mainDeckCardCounts[entry.cardId] = entry.quantity;
                }
            }

            if (_currentDeck?.extraDeckCards != null)
            {
                foreach (var entry in _currentDeck.extraDeckCards)
                {
                    _extraDeckCardCounts[entry.cardId] = entry.quantity;
                }
            }
        }

        private void UpdateDeckDisplay()
        {
            if (_deckNameDisplay != null)
            {
                _deckNameDisplay.text = _currentDeck?.deckName ?? "未命名卡组";
            }

            if (_deckNameInput != null)
            {
                _deckNameInput.SetValueWithoutNotify(_currentDeck?.deckName ?? "");
            }

            if (_deckDescInput != null)
            {
                _deckDescInput.SetValueWithoutNotify(_currentDeck?.description ?? "");
            }

            RefreshMainDeck();
            RefreshExtraDeck();
        }

        private void RefreshMainDeck()
        {
            if (_mainDeckCards == null) return;

            _mainDeckCards.Clear();

            foreach (var kvp in _mainDeckCardCounts)
            {
                var card = GetCardById(kvp.Key);
                if (card != null && kvp.Value > 0)
                {
                    var item = CreateDeckCardSlot(card, kvp.Value, false);
                    _mainDeckCards.Add(item);
                }
            }

            // 更新数量显示
            int count = _mainDeckCardCounts.Values.Sum();
            _mainDeckCount.text = $"{count}/60";

            bool isValid = count >= 40 && count <= 60;
            _mainDeckCount.EnableInClassList("valid", isValid);
            _mainDeckCount.EnableInClassList("invalid", !isValid);
        }

        private void RefreshExtraDeck()
        {
            if (_extraDeckCards == null) return;

            _extraDeckCards.Clear();

            foreach (var kvp in _extraDeckCardCounts)
            {
                var card = GetCardById(kvp.Key);
                if (card != null && kvp.Value > 0)
                {
                    var item = CreateDeckCardSlot(card, kvp.Value, true);
                    _extraDeckCards.Add(item);
                }
            }

            // 更新数量显示
            int count = _extraDeckCardCounts.Values.Sum();
            _extraDeckCount.text = $"{count}/15";

            bool isValid = count <= 15;
            _extraDeckCount.EnableInClassList("valid", isValid);
            _extraDeckCount.EnableInClassList("invalid", !isValid);
        }

        private VisualElement CreateDeckCardSlot(TempCardData card, int quantity, bool isExtraDeck)
        {
            var item = new VisualElement { name = $"deck-slot-{card.id}" };
            item.AddToClassList("deck-card-slot");

            // 图标
            var icon = new VisualElement { name = "card-icon" };
            icon.AddToClassList("deck-card-slot-icon");
            icon.AddToClassList(GetCardTypeClass(card.cardType));
            item.Add(icon);

            // 信息
            var info = new VisualElement { name = "card-info" };
            info.AddToClassList("deck-card-slot-info");

            var name = new Label(card.cardName) { name = "card-name" };
            name.AddToClassList("deck-card-slot-name");
            info.Add(name);

            var cost = new Label(card.GetCostString()) { name = "card-cost" };
            cost.AddToClassList("deck-card-slot-cost");
            info.Add(cost);

            item.Add(info);

            // 数量控制
            var quantityContainer = new VisualElement { name = "quantity" };
            quantityContainer.AddToClassList("deck-card-slot-quantity");

            var btnMinus = new Button(() => RemoveCardFromDeck(card, isExtraDeck)) { text = "-" };
            btnMinus.AddToClassList("btn");
            btnMinus.AddToClassList("btn-sm");
            quantityContainer.Add(btnMinus);

            var countLabel = new Label(quantity.ToString()) { name = "count" };
            countLabel.AddToClassList("deck-card-slot-count");
            quantityContainer.Add(countLabel);

            var btnPlus = new Button(() => AddCardToDeck(card, isExtraDeck)) { text = "+" };
            btnPlus.AddToClassList("btn");
            btnPlus.AddToClassList("btn-sm");
            quantityContainer.Add(btnPlus);

            item.Add(quantityContainer);

            return item;
        }

        private TempCardData GetCardById(string cardId)
        {
            return _availableCardList.FirstOrDefault(c => c.id == cardId);
        }

        private void AddCardToDeck(TempCardData card, bool? forceExtraDeck = null)
        {
            // 确定是主卡组还是额外卡组
            bool isExtraDeck = forceExtraDeck ?? (card.cardType == CardType.传奇);

            var targetCounts = isExtraDeck ? _extraDeckCardCounts : _mainDeckCardCounts;

            // 检查数量限制
            int currentTotal = targetCounts.Values.Sum();
            int maxCount = isExtraDeck ? 15 : 60;
            int maxPerCard = card.isLegendary ? 1 : 4;

            if (currentTotal >= maxCount)
            {
                Debug.Log($"{(isExtraDeck ? "额外卡组" : "主卡组")}已满");
                return;
            }

            int currentCount = targetCounts.GetValueOrDefault(card.id, 0);
            if (currentCount >= maxPerCard)
            {
                Debug.Log($"卡牌 {card.cardName} 已达到最大数量");
                return;
            }

            targetCounts[card.id] = currentCount + 1;
            _isDirty = true;

            if (isExtraDeck)
            {
                RefreshExtraDeck();
            }
            else
            {
                RefreshMainDeck();
            }

            UpdateStatistics();
        }

        private void RemoveCardFromDeck(TempCardData card, bool isExtraDeck)
        {
            var targetCounts = isExtraDeck ? _extraDeckCardCounts : _mainDeckCardCounts;

            int currentCount = targetCounts.GetValueOrDefault(card.id, 0);
            if (currentCount > 0)
            {
                targetCounts[card.id] = currentCount - 1;
                _isDirty = true;

                if (isExtraDeck)
                {
                    RefreshExtraDeck();
                }
                else
                {
                    RefreshMainDeck();
                }

                UpdateStatistics();
            }
        }

        private void UpdateStatistics()
        {
            var allCards = new List<TempCardData>();

            foreach (var kvp in _mainDeckCardCounts)
            {
                var card = GetCardById(kvp.Key);
                for (int i = 0; i < kvp.Value; i++)
                {
                    allCards.Add(card);
                }
            }

            foreach (var kvp in _extraDeckCardCounts)
            {
                var card = GetCardById(kvp.Key);
                for (int i = 0; i < kvp.Value; i++)
                {
                    allCards.Add(card);
                }
            }

            // 统计数量
            _statTotal.text = allCards.Count.ToString();
            _statCreatures.text = allCards.Count(c => c.cardType == CardType.生物).ToString();
            _statSpells.text = allCards.Count(c => c.cardType == CardType.术法).ToString();
            _statDomains.text = allCards.Count(c => c.cardType == CardType.领域).ToString();
            _statLegendaries.text = allCards.Count(c => c.cardType == CardType.传奇).ToString();

            // 更新颜色分布
            UpdateColorDistribution(allCards);
        }

        private void UpdateColorDistribution(List<TempCardData> cards)
        {
            if (_colorDistribution == null) return;

            var colorCounts = new Dictionary<ManaType, int>
            {
                { ManaType.红色, 0 },
                { ManaType.蓝色, 0 },
                { ManaType.绿色, 0 },
                { ManaType.白色, 0 },
                { ManaType.黑色, 0 }
            };

            foreach (var card in cards)
            {
                foreach (var cost in card.manaCost)
                {
                    if (colorCounts.ContainsKey(cost.manaType))
                    {
                        colorCounts[cost.manaType] += cost.amount;
                    }
                }
            }

            int maxCount = colorCounts.Values.Max();
            if (maxCount == 0) maxCount = 1;

            UpdateColorBar("color-red-bar", colorCounts[ManaType.红色], maxCount);
            UpdateColorBar("color-blue-bar", colorCounts[ManaType.蓝色], maxCount);
            UpdateColorBar("color-green-bar", colorCounts[ManaType.绿色], maxCount);
            UpdateColorBar("color-white-bar", colorCounts[ManaType.白色], maxCount);
            UpdateColorBar("color-black-bar", colorCounts[ManaType.黑色], maxCount);
        }

        private void UpdateColorBar(string barName, int count, int maxCount)
        {
            var bar = _colorDistribution.Q(barName);
            if (bar == null) return;

            var fill = bar.Q<VisualElement>("color-fill");
            if (fill != null)
            {
                float percentage = (float)count / maxCount * 100;
                fill.style.width = Length.Percent(percentage);
            }
        }

        #region 拖拽处理

        private TempCardData _dragCard;
        private bool _isDragging;

        private void OnCardDragStart(MouseDownEvent evt, TempCardData card)
        {
            _dragCard = card;
            _isDragging = true;
        }

        private void OnCardDragMove(MouseMoveEvent evt)
        {
            if (!_isDragging) return;
            // 拖拽中
        }

        private void OnCardDragEnd(MouseUpEvent evt)
        {
            _isDragging = false;
            _dragCard = null;

            _mainDeckArea?.RemoveFromClassList("drag-over");
            _extraDeckArea?.RemoveFromClassList("drag-over");
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            var target = evt.target as VisualElement;
            target?.AddToClassList("drag-over");
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            var target = evt.target as VisualElement;
            target?.RemoveFromClassList("drag-over");
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            // 更新拖拽状态
        }

        private void OnDragPerformMainDeck(DragPerformEvent evt)
        {
            _mainDeckArea?.RemoveFromClassList("drag-over");

            if (_dragCard != null)
            {
                AddCardToDeck(_dragCard, false);
            }
        }

        private void OnDragPerformExtraDeck(DragPerformEvent evt)
        {
            _extraDeckArea?.RemoveFromClassList("drag-over");

            if (_dragCard != null)
            {
                AddCardToDeck(_dragCard, true);
            }
        }

        #endregion

        #region 事件处理

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            RefreshAvailableCards();
        }

        private void OnFilterClick(ClickEvent evt)
        {
            var btn = evt.target as Button;
            if (btn == null) return;

            // 更新筛选标签状态
            var filterButtons = _filterTags?.Query<Button>().ToList();
            if (filterButtons != null)
            {
                foreach (var b in filterButtons)
                {
                    b.RemoveFromClassList("active");
                }
            }
            btn.AddToClassList("active");

            _currentFilter = btn.text;
            RefreshAvailableCards();
        }

        private void OnDeckNameChanged(ChangeEvent<string> evt)
        {
            if (_currentDeck == null) return;
            _currentDeck.deckName = evt.newValue;
            _deckNameDisplay.text = evt.newValue;
            _isDirty = true;
        }

        private void OnDeckDescChanged(ChangeEvent<string> evt)
        {
            if (_currentDeck == null) return;
            _currentDeck.description = evt.newValue;
            _isDirty = true;
        }

        private void OnSaveDeckClick(ClickEvent evt)
        {
            SaveDeck();
        }

        private void OnExportDeckClick(ClickEvent evt)
        {
            ExportDeck();
        }

        private void OnDeleteDeckClick(ClickEvent evt)
        {
            DeleteDeck();
        }

        #endregion

        private void SaveDeck()
        {
            if (_currentDeck == null) return;

            // 同步卡牌计数到卡组数据
            _currentDeck.mainDeckCards.Clear();
            foreach (var kvp in _mainDeckCardCounts)
            {
                if (kvp.Value > 0)
                {
                    _currentDeck.mainDeckCards.Add(new DeckCardEntry { cardId = kvp.Key, quantity = kvp.Value });
                }
            }

            _currentDeck.extraDeckCards.Clear();
            foreach (var kvp in _extraDeckCardCounts)
            {
                if (kvp.Value > 0)
                {
                    _currentDeck.extraDeckCards.Add(new DeckCardEntry { cardId = kvp.Key, quantity = kvp.Value });
                }
            }

            Debug.Log($"保存卡组: {_currentDeck.deckName}");
            _isDirty = false;
        }

        private void ExportDeck()
        {
            if (_currentDeck == null) return;

            var json = JsonUtility.ToJson(_currentDeck, true);
            Debug.Log($"导出卡组:\n{json}");

            // TODO: 实际导出到文件
        }

        private void DeleteDeck()
        {
            // TODO: 确认删除对话框
            Debug.Log("删除卡组");
        }
    }
}