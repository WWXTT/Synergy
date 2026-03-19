using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using CardCore.Data;
using cfg;

namespace CardCore.UI.UIToolkit
{
    /// <summary>
    /// 卡牌编辑器面板
    /// 使用 UI Toolkit 实现的三栏布局卡牌编辑界面
    /// </summary>
    public class CardEditorPanel : BaseUIToolkitPanel
    {
        [Header("数据引用")]
        [SerializeField] private TempCardDataSO _cardDataSO;
        [SerializeField] private TempEffectDataSO _effectDataSO;

        // UI 元素引用
        private TextField _searchInput;
        private DropdownField _typeFilter;
        private ScrollView _cardList;
        private Button _btnNewCard;

        private TextField _cardNameInput;
        private DropdownField _cardTypeDropdown;
        private Toggle _isLegendaryToggle;
        private IntegerField _powerInput;
        private IntegerField _lifeInput;
        private VisualElement _combatSection;

        private IntegerField _grayCostInput;
        private IntegerField _redCostInput;
        private IntegerField _blueCostInput;
        private IntegerField _greenCostInput;
        private IntegerField _whiteCostInput;
        private IntegerField _blackCostInput;

        private VisualElement _effectList;
        private Button _btnAddEffect;
        private TextField _descriptionInput;

        private Button _btnReset;
        private Button _btnSave;

        private VisualElement _cardPreview;

        // 数据
        private List<TempCardData> _cards = new List<TempCardData>();
        private TempCardData _currentCard;
        private bool _isDirty;

        protected override void Awake()
        {
            base.Awake();
            _panelName = "CardEditor";
        }

        protected override void BindUIElements()
        {
            base.BindUIElements();

            // 左侧面板
            _searchInput = Q<TextField>("search-input");
            _typeFilter = Q<DropdownField>("type-filter");
            _cardList = Q<ScrollView>("card-list");
            _btnNewCard = Q<Button>("btn-new-card");

            // 中间编辑区域
            _cardNameInput = Q<TextField>("card-name-input");
            _cardTypeDropdown = Q<DropdownField>("card-type-dropdown");
            _isLegendaryToggle = Q<Toggle>("is-legendary-toggle");
            _powerInput = Q<IntegerField>("power-input");
            _lifeInput = Q<IntegerField>("life-input");
            _combatSection = Q("combat-section");

            // 法力费用
            _grayCostInput = Q<IntegerField>("gray-cost-input");
            _redCostInput = Q<IntegerField>("red-cost-input");
            _blueCostInput = Q<IntegerField>("blue-cost-input");
            _greenCostInput = Q<IntegerField>("green-cost-input");
            _whiteCostInput = Q<IntegerField>("white-cost-input");
            _blackCostInput = Q<IntegerField>("black-cost-input");

            // 效果和描述
            _effectList = Q("effect-list");
            _btnAddEffect = Q<Button>("btn-add-effect");
            _descriptionInput = Q<TextField>("description-input");

            // 按钮
            _btnReset = Q<Button>("btn-reset");
            _btnSave = Q<Button>("btn-save");

            // 预览
            _cardPreview = Q("card-preview");
        }

        protected override void RegisterEvents()
        {
            base.RegisterEvents();

            // 搜索和筛选
            _searchInput?.RegisterValueChangedCallback(OnSearchChanged);
            _typeFilter?.RegisterValueChangedCallback(OnTypeFilterChanged);

            // 新建卡牌
            _btnNewCard?.RegisterCallback<ClickEvent>(OnNewCardClick);

            // 基本信息编辑
            _cardNameInput?.RegisterValueChangedCallback(OnCardNameChanged);
            _cardTypeDropdown?.RegisterValueChangedCallback(OnCardTypeChanged);
            _isLegendaryToggle?.RegisterValueChangedCallback(OnLegendaryChanged);

            // 战斗属性
            _powerInput?.RegisterValueChangedCallback(OnPowerChanged);
            _lifeInput?.RegisterValueChangedCallback(OnLifeChanged);

            // 法力费用
            _grayCostInput?.RegisterValueChangedCallback(OnManaCostChanged);
            _redCostInput?.RegisterValueChangedCallback(OnManaCostChanged);
            _blueCostInput?.RegisterValueChangedCallback(OnManaCostChanged);
            _greenCostInput?.RegisterValueChangedCallback(OnManaCostChanged);
            _whiteCostInput?.RegisterValueChangedCallback(OnManaCostChanged);
            _blackCostInput?.RegisterValueChangedCallback(OnManaCostChanged);

            // 效果
            _btnAddEffect?.RegisterCallback<ClickEvent>(OnAddEffectClick);

            // 描述
            _descriptionInput?.RegisterValueChangedCallback(OnDescriptionChanged);

            // 保存/重置
            _btnReset?.RegisterCallback<ClickEvent>(OnResetClick);
            _btnSave?.RegisterCallback<ClickEvent>(OnSaveClick);

            // 初始化下拉框选项
            InitializeDropdowns();
        }

        private void InitializeDropdowns()
        {
            // 卡牌类型筛选
            if (_typeFilter != null)
            {
                _typeFilter.choices = new List<string> { "全部", "生物", "传奇", "术法", "领域" };
                _typeFilter.value = "全部";
            }

            // 卡牌类型选择
            if (_cardTypeDropdown != null)
            {
                _cardTypeDropdown.choices = new List<string> { "生物", "传奇", "术法", "领域" };
                _cardTypeDropdown.value = "生物";
            }
        }

        public override void Refresh()
        {
            base.Refresh();
            LoadCards();
        }

        private void LoadCards()
        {
            _cards.Clear();

            if (_cardDataSO != null)
            {
                _cards = new List<TempCardData>(_cardDataSO.cards);
            }

            RefreshCardList();
        }

        private void RefreshCardList()
        {
            if (_cardList == null) return;

            _cardList.Clear();

            foreach (var card in _cards)
            {
                var item = CreateCardListItem(card);
                _cardList.Add(item);
            }
        }

        private VisualElement CreateCardListItem(TempCardData card)
        {
            var item = new VisualElement { name = $"card-item-{card.id}" };
            item.AddToClassList("card-list-item");

            // 图标
            var icon = new VisualElement { name = "card-icon" };
            icon.AddToClassList("card-list-item-icon");
            icon.AddToClassList(GetCardTypeClass(card.cardType));
            item.Add(icon);

            // 内容
            var content = new VisualElement { name = "card-content" };
            content.AddToClassList("card-list-item-content");

            var name = new Label(card.cardName) { name = "card-name" };
            name.AddToClassList("card-list-item-name");
            content.Add(name);

            var info = new Label($"{card.cardType} · {card.GetTotalCost()}费") { name = "card-info" };
            info.AddToClassList("card-list-item-info");
            content.Add(info);

            item.Add(content);

            // 费用
            var cost = new Label(card.GetTotalCost().ToString()) { name = "card-cost" };
            cost.AddToClassList("card-list-item-cost");
            item.Add(cost);

            // 点击事件
            item.RegisterCallback<ClickEvent>(evt => SelectCard(card));

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

        private void SelectCard(TempCardData card)
        {
            _currentCard = card;
            UpdateEditPanel();
            UpdatePreview();
            HighlightSelectedCard();
        }

        private void HighlightSelectedCard()
        {
            if (_cardList == null) return;

            foreach (var child in _cardList.Children())
            {
                child.RemoveFromClassList("selected");
            }

            if (_currentCard != null)
            {
                var selectedItem = _cardList.Q($"card-item-{_currentCard.id}");
                selectedItem?.AddToClassList("selected");
            }
        }

        private void UpdateEditPanel()
        {
            if (_currentCard == null) return;

            // 基本信息
            _cardNameInput.SetValueWithoutNotify(_currentCard.cardName);
            _cardTypeDropdown.SetValueWithoutNotify(_currentCard.cardType.ToString());
            _isLegendaryToggle.SetValueWithoutNotify(_currentCard.isLegendary);

            // 战斗属性
            _powerInput.SetValueWithoutNotify(_currentCard.power ?? 0);
            _lifeInput.SetValueWithoutNotify(_currentCard.life ?? 0);
            UpdateCombatSectionVisibility();

            // 法力费用
            UpdateManaCostInputs();

            // 效果列表
            RefreshEffectList();

            // 描述
            _descriptionInput.SetValueWithoutNotify(_currentCard.description ?? "");

            _isDirty = false;
        }

        private void UpdateCombatSectionVisibility()
        {
            if (_combatSection == null) return;

            bool showCombat = _currentCard?.cardType == CardType.生物 ||
                             _currentCard?.cardType == CardType.传奇;

            _combatSection.style.display = showCombat ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateManaCostInputs()
        {
            if (_currentCard == null) return;

            var costs = _currentCard.manaCost;

            _grayCostInput.SetValueWithoutNotify(GetManaAmount(costs, ManaType.灰色));
            _redCostInput.SetValueWithoutNotify(GetManaAmount(costs, ManaType.红色));
            _blueCostInput.SetValueWithoutNotify(GetManaAmount(costs, ManaType.蓝色));
            _greenCostInput.SetValueWithoutNotify(GetManaAmount(costs, ManaType.绿色));
            _whiteCostInput.SetValueWithoutNotify(GetManaAmount(costs, ManaType.白色));
            _blackCostInput.SetValueWithoutNotify(GetManaAmount(costs, ManaType.黑色));
        }

        private int GetManaAmount(List<ManaCostEntry> costs, ManaType type)
        {
            foreach (var cost in costs)
            {
                if (cost.manaType == type)
                    return cost.amount;
            }
            return 0;
        }

        private void RefreshEffectList()
        {
            if (_effectList == null || _currentCard == null) return;

            _effectList.Clear();

            if (_currentCard.effects == null || _currentCard.effects.Count == 0)
            {
                var emptyLabel = new Label("暂无效果") { name = "empty-label" };
                emptyLabel.AddToClassList("empty-state-text");
                _effectList.Add(emptyLabel);
                return;
            }

            foreach (var effect in _currentCard.effects)
            {
                var item = CreateEffectListItem(effect);
                _effectList.Add(item);
            }
        }

        private VisualElement CreateEffectListItem(TempEffectRef effect)
        {
            var item = new VisualElement { name = $"effect-item-{effect.effectId}" };
            item.AddToClassList("effect-list-item");

            var name = new Label(effect.effectId) { name = "effect-name" };
            name.AddToClassList("effect-name");
            item.Add(name);

            var paramsLabel = new Label(effect.parameters ?? "") { name = "effect-params" };
            paramsLabel.AddToClassList("effect-brief");
            item.Add(paramsLabel);

            var deleteBtn = new Button(() => RemoveEffect(effect)) { text = "删除" };
            deleteBtn.AddToClassList("btn");
            deleteBtn.AddToClassList("btn-sm");
            deleteBtn.AddToClassList("btn-danger");
            item.Add(deleteBtn);

            return item;
        }

        private void UpdatePreview()
        {
            if (_cardPreview == null || _currentCard == null) return;

            _cardPreview.Clear();

            // 创建预览卡片
            var previewCard = CreatePreviewCard();
            _cardPreview.Add(previewCard);
        }

        private VisualElement CreatePreviewCard()
        {
            var card = new VisualElement { name = "preview-card" };
            card.AddToClassList("card-preview-card");

            var frame = new VisualElement { name = "card-frame" };
            frame.AddToClassList("card-frame");
            frame.AddToClassList(GetCardTypeClass(_currentCard.cardType));

            // 头部
            var header = new VisualElement { name = "card-header" };
            header.AddToClassList("card-header");

            var name = new Label(_currentCard.cardName) { name = "card-name" };
            name.AddToClassList("card-name");
            header.Add(name);

            var costDisplay = CreateManaCostDisplay();
            header.Add(costDisplay);

            frame.Add(header);

            // 图像区域
            var imageContainer = new VisualElement { name = "image-container" };
            imageContainer.AddToClassList("card-image-container");
            frame.Add(imageContainer);

            // 类型栏
            var typeBar = new VisualElement { name = "type-bar" };
            typeBar.AddToClassList("card-type-bar");

            var typeLabel = new Label(_currentCard.cardType.ToString()) { name = "card-type" };
            typeLabel.AddToClassList("card-type-text");
            typeBar.Add(typeLabel);

            if (_currentCard.isLegendary)
            {
                var legendaryBadge = new VisualElement { name = "legendary-badge" };
                legendaryBadge.AddToClassList("legendary-badge");
                var legendaryText = new Label("传奇");
                legendaryText.AddToClassList("legendary-text");
                legendaryBadge.Add(legendaryText);
                typeBar.Add(legendaryBadge);
            }

            frame.Add(typeBar);

            // 描述
            var textBox = new VisualElement { name = "text-box" };
            textBox.AddToClassList("card-text-box");
            var desc = new Label(_currentCard.description ?? "") { name = "card-description" };
            desc.AddToClassList("card-description");
            textBox.Add(desc);
            frame.Add(textBox);

            // 战斗属性
            if (_currentCard.cardType == CardType.生物 || _currentCard.cardType == CardType.传奇)
            {
                var stats = new VisualElement { name = "combat-stats" };
                stats.AddToClassList("combat-stats");

                var power = new Label(_currentCard.power?.ToString() ?? "0") { name = "power-value" };
                power.AddToClassList("power-value");
                stats.Add(power);

                var separator = new Label("/") { name = "separator" };
                separator.AddToClassList("stats-separator");
                stats.Add(separator);

                var life = new Label(_currentCard.life?.ToString() ?? "0") { name = "life-value" };
                life.AddToClassList("life-value");
                stats.Add(life);

                frame.Add(stats);
            }

            card.Add(frame);
            return card;
        }

        private VisualElement CreateManaCostDisplay()
        {
            var display = new VisualElement { name = "mana-cost-display" };
            display.AddToClassList("mana-cost-display");

            if (_currentCard?.manaCost == null) return display;

            foreach (var cost in _currentCard.manaCost)
            {
                if (cost.amount > 0)
                {
                    var orb = new VisualElement { name = $"mana-{cost.manaType}" };
                    orb.AddToClassList("mana-orb");
                    orb.AddToClassList($"mana-{cost.manaType.ToString().ToLower()}");

                    var count = new Label(cost.amount.ToString()) { name = "mana-count" };
                    count.AddToClassList("mana-count");
                    orb.Add(count);

                    display.Add(orb);
                }
            }

            return display;
        }

        #region 事件处理

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            // TODO: 实现搜索过滤
        }

        private void OnTypeFilterChanged(ChangeEvent<string> evt)
        {
            // TODO: 实现类型筛选
        }

        private void OnNewCardClick(ClickEvent evt)
        {
            var newCard = new TempCardData
            {
                id = $"CARD_{DateTime.Now.Ticks}",
                cardName = "新卡牌",
                cardType = CardType.生物,
                isLegendary = false,
                power = 1,
                life = 1,
                description = ""
            };

            _cards.Add(newCard);
            RefreshCardList();
            SelectCard(newCard);
            _isDirty = true;
        }

        private void OnCardNameChanged(ChangeEvent<string> evt)
        {
            if (_currentCard == null) return;
            _currentCard.cardName = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnCardTypeChanged(ChangeEvent<string> evt)
        {
            if (_currentCard == null) return;

            if (Enum.TryParse<CardType>(evt.newValue, out var type))
            {
                _currentCard.cardType = type;
                UpdateCombatSectionVisibility();
                _isDirty = true;
                UpdatePreview();
            }
        }

        private void OnLegendaryChanged(ChangeEvent<bool> evt)
        {
            if (_currentCard == null) return;
            _currentCard.isLegendary = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnPowerChanged(ChangeEvent<int> evt)
        {
            if (_currentCard == null) return;
            _currentCard.power = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnLifeChanged(ChangeEvent<int> evt)
        {
            if (_currentCard == null) return;
            _currentCard.life = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnManaCostChanged(ChangeEvent<int> evt)
        {
            if (_currentCard == null) return;

            // 更新法力费用
            _currentCard.manaCost.Clear();

            if (_grayCostInput.value > 0)
                _currentCard.manaCost.Add(new ManaCostEntry { manaType = ManaType.灰色, amount = _grayCostInput.value });
            if (_redCostInput.value > 0)
                _currentCard.manaCost.Add(new ManaCostEntry { manaType = ManaType.红色, amount = _redCostInput.value });
            if (_blueCostInput.value > 0)
                _currentCard.manaCost.Add(new ManaCostEntry { manaType = ManaType.蓝色, amount = _blueCostInput.value });
            if (_greenCostInput.value > 0)
                _currentCard.manaCost.Add(new ManaCostEntry { manaType = ManaType.绿色, amount = _greenCostInput.value });
            if (_whiteCostInput.value > 0)
                _currentCard.manaCost.Add(new ManaCostEntry { manaType = ManaType.白色, amount = _whiteCostInput.value });
            if (_blackCostInput.value > 0)
                _currentCard.manaCost.Add(new ManaCostEntry { manaType = ManaType.黑色, amount = _blackCostInput.value });

            _isDirty = true;
            UpdatePreview();
        }

        private void OnAddEffectClick(ClickEvent evt)
        {
            // TODO: 打开效果选择弹窗
            Debug.Log("添加效果");
        }

        private void RemoveEffect(TempEffectRef effect)
        {
            if (_currentCard == null) return;
            _currentCard.effects.Remove(effect);
            RefreshEffectList();
            _isDirty = true;
        }

        private void OnDescriptionChanged(ChangeEvent<string> evt)
        {
            if (_currentCard == null) return;
            _currentCard.description = evt.newValue;
            _isDirty = true;
            UpdatePreview();
        }

        private void OnResetClick(ClickEvent evt)
        {
            if (_currentCard != null)
            {
                UpdateEditPanel();
                _isDirty = false;
            }
        }

        private void OnSaveClick(ClickEvent evt)
        {
            SaveCard();
        }

        #endregion

        private void SaveCard()
        {
            if (_currentCard == null) return;

            // TODO: 实际保存逻辑
            Debug.Log($"保存卡牌: {_currentCard.cardName}");
            _isDirty = false;

            // 刷���列表
            RefreshCardList();
        }
    }
}