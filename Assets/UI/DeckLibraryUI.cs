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
    /// 牌库展示 UI - 用于显示和管理所有牌组
    /// </summary>
    public class DeckLibraryUI : BaseUI
    {
        [Header("牌组列表")]
        [SerializeField] private Transform _deckContainer;
        [SerializeField] private GameObject _deckItemPrefab;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("搜索")]
        [SerializeField] private TMP_InputField _searchInput;
        [SerializeField] private Button _searchButton;

        [Header("统计信息")]
        [SerializeField] private TMP_Text _totalDecksText;
        [SerializeField] private TMP_Text _totalCardsText;

        [Header("牌组详情面板")]
        [SerializeField] private GameObject _deckDetailPanel;
        [SerializeField] private TMP_Text _deckNameText;
        [SerializeField] private TMP_Text _deckDescriptionText;
        [SerializeField] private TMP_Text _deckCardCountText;
        [SerializeField] private TMP_Text _deckCreationTimeText;
        [SerializeField] private TMP_Text _deckModifiedTimeText;
        [SerializeField] private Transform _deckCardsContainer;
        [SerializeField] private GameObject _deckCardItemPrefab;
        [SerializeField] private Button _closeDetailButton;

        [Header("按钮")]
        [SerializeField] private Button _createNewDeckButton;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _editDeckButton;
        [SerializeField] private Button _deleteDeckButton;
        [SerializeField] private Button _exportDeckButton;
        [SerializeField] private Button _importDeckButton;

        // 牌组管理器引用
        private CardDeckManager _deckManager;

        // 牌组列表项
        private List<DeckListItemUI> _deckItems = new List<DeckListItemUI>();
        private List<GameObject> _currentDeckCards = new List<GameObject>();

        // 选中的牌组
        private DeckListItemUI _selectedDeckItem;
        private CardDeck _selectedDeck;

        // 牌组数据（临时存储，实际应该从文件加载）
        private List<CardDeck> _decks = new List<CardDeck>();

        public DeckListItemUI SelectedDeckItem => _selectedDeckItem;
        public CardDeck SelectedDeck => _selectedDeck;

        // 牌组选中事件
        public event Action<CardDeck> OnDeckSelected;
        public event Action<CardDeck> OnDeckEditRequested;
        public event Action<CardDeck> OnDeckDeleteRequested;

        protected override void Initialize()
        {
            base.Initialize();

            // 绑定按钮事件
            _createNewDeckButton?.AddClickListener(OnCreateNewDeckClicked);
            _refreshButton?.AddClickListener(OnRefreshClicked);
            _editDeckButton?.AddClickListener(OnEditDeckClicked);
            _deleteDeckButton?.AddClickListener(OnDeleteDeckClicked);
            _exportDeckButton?.AddClickListener(OnExportDeckClicked);
            _importDeckButton?.AddClickListener(OnImportDeckClicked);
            _closeDetailButton?.AddClickListener(OnCloseDetailClicked);

            // 绑定搜索事件
            _searchButton?.AddClickListener(OnSearchClicked);
            if (_searchInput != null)
            {
                _searchInput.onEndEdit.AddListener(OnSearchSubmitted);
            }

            // 隐藏详情面板
            if (_deckDetailPanel != null)
            {
                _deckDetailPanel.SetActive(false);
            }

            // 获取牌组管理器
            _deckManager = Resources.Load<CardDeckManager>("CardDecks");

            // 如果没有找到，使用默认空管理器
            if (_deckManager == null)
            {
                _deckManager = ScriptableObject.CreateInstance<CardDeckManager>();
            }

            // 禁用操作按钮（初始状态）
            SetDeckOperationButtonsEnabled(false);
        }

        /// <summary>
        /// 刷新牌组列表
        /// </summary>
        public void RefreshDeckList()
        {
            RefreshDeckListAsync().Forget();
        }

        /// <summary>
        /// 刷新牌组列表异步
        /// </summary>
        private async UniTaskVoid RefreshDeckListAsync()
        {
            UIManager.Instance.ShowLoading(true);

            // 确保卡牌注册表已加载
            if (!CardDataRegistry.Instance.IsLoaded)
            {
                await CardDataRegistry.Instance.LoadAsync();
            }

            // 加载牌组数据
            LoadDecks();

            // 清除现有列表
            ClearDeckList();

            // 创建牌组列表项
            foreach (var deck in _decks)
            {
                CreateDeckListItem(deck);
            }

            // 更新统计信息
            UpdateStatistics();

            UIManager.Instance.ShowLoading(false);
        }

        /// <summary>
        /// 加载牌组数据
        /// </summary>
        private void LoadDecks()
        {
            _decks.Clear();

            // 从 CardDeckManager 加载牌组
            if (_deckManager != null)
            {
                _decks = _deckManager.GetAllDecks();
            }
            else
            {
                // 如果没有管理器，从本地文件加载
                LoadDecksFromFile();
            }
        }

        /// <summary>
        /// 从文件加载牌组
        /// </summary>
        private void LoadDecksFromFile()
        {
            string savePath = System.IO.Path.Combine(
                UnityEngine.Application.persistentDataPath,
                "Decks",
                "decks.json"
            );

            if (System.IO.File.Exists(savePath))
            {
                try
                {
                    string json =  System.IO.File.ReadAllText(savePath);

                    var deckList = JsonUtility.FromJson<DeckListWrapper>(json);
                    if (deckList != null && deckList.Decks != null)
                    {
                        _decks = deckList.Decks;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"加载牌组失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 创建牌组列表项
        /// </summary>
        private void CreateDeckListItem(CardDeck deck)
        {
            if (_deckItemPrefab == null || _deckContainer == null) return;

            GameObject itemObj = Instantiate(_deckItemPrefab, _deckContainer);
            var itemUI = itemObj.GetComponent<DeckListItemUI>();

            if (itemUI != null)
            {
                itemUI.Initialize(deck, this);
                _deckItems.Add(itemUI);
            }
        }

        /// <summary>
        /// 清除牌组列表
        /// </summary>
        private void ClearDeckList()
        {
            foreach (var item in _deckItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
            }
            _deckItems.Clear();
            _selectedDeckItem = null;
            _selectedDeck = null;
            SetDeckOperationButtonsEnabled(false);
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            if (_totalDecksText != null)
            {
                _totalDecksText.text = $"总牌组数: {_decks.Count}";
            }

            // 计算总卡牌数
            int totalCards = 0;
            foreach (var deck in _decks)
            {
                totalCards += deck.CardCount;
            }

            if (_totalCardsText != null)
            {
                _totalCardsText.text = $"总卡牌数: {totalCards}";
            }
        }

        /// <summary>
        /// 设置牌组操作按钮启用状态
        /// </summary>
        private void SetDeckOperationButtonsEnabled(bool enabled)
        {
            if (_editDeckButton != null)
                _editDeckButton.interactable = enabled;

            if (_deleteDeckButton != null)
                _deleteDeckButton.interactable = enabled;

            if (_exportDeckButton != null)
                _exportDeckButton.interactable = enabled;
        }

        /// <summary>
        /// 显示牌组详情
        /// </summary>
        private void ShowDeckDetail(CardDeck deck)
        {
            if (deck == null || _deckDetailPanel == null) return;

            _selectedDeck = deck;

            // 更新牌组信息
            _deckNameText.text = deck.DeckName ?? "未命名牌组";
            _deckDescriptionText.text = deck.Description ?? "无描述";
            _deckCardCountText.text = $"卡牌数: {deck.CardCount}";
            _deckCreationTimeText.text = $"创建时间: {deck.CreatedTime:yyyy-MM-dd HH:mm}";
            _deckModifiedTimeText.text = $"修改时间: {deck.ModifiedTime:yyyy-MM-dd HH:mm}";

            // 清除并重新创建卡牌列表
            ClearDeckCards();

            // 获取牌组中的卡牌
            var cards = deck.GetCards();
            foreach (var card in cards)
            {
                CreateDeckCardItem(card);
            }

            // 显示详情面板
            _deckDetailPanel.SetActive(true);
        }

        /// <summary>
        /// 清除牌组卡牌列表
        /// </summary>
        private void ClearDeckCards()
        {
            foreach (var cardObj in _currentDeckCards)
            {
                if (cardObj != null)
                {
                    Destroy(cardObj);
                }
            }
            _currentDeckCards.Clear();
        }

        /// <summary>
        /// 创建牌组卡牌项
        /// </summary>
        private void CreateDeckCardItem(CardData cardData)
        {
            if (_deckCardItemPrefab == null || _deckCardsContainer == null) return;

            GameObject itemObj = Instantiate(_deckCardItemPrefab, _deckCardsContainer);

            // 设置卡牌信息
            TextMeshProUGUI[] textMeshes = itemObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (textMeshes != null && textMeshes.Length > 0)
            {
                textMeshes[0].text = cardData.CardName ?? "未命名";
            }

            // 设置卡牌类型颜色
            Image[] images = itemObj.GetComponentsInChildren<Image>();
            if (images != null && images.Length > 1)
            {
                images[1].color = GetCardTypeColor(cardData.CardType);
            }

            _currentDeckCards.Add(itemObj);
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

        // ==================== 事件处理 ====================

        /// <summary>
        /// 搜索提交时调用
        /// </summary>
        private void OnSearchSubmitted(string value)
        {
            OnSearchClicked();
        }

        /// <summary>
        /// 搜索按钮点击
        /// </summary>
        private void OnSearchClicked()
        {
            string searchQuery = _searchInput?.text ?? string.Empty;
            searchQuery = searchQuery.ToLower();

            // 过滤牌组
            List<CardDeck> filteredDecks = new List<CardDeck>();
            foreach (var deck in _decks)
            {
                if ((deck.DeckName?.ToLower().Contains(searchQuery) ?? false) ||
                    (deck.Description?.ToLower().Contains(searchQuery) ?? false))
                {
                    filteredDecks.Add(deck);
                }
            }

            // 重新创建列表
            ClearDeckList();
            foreach (var deck in filteredDecks)
            {
                CreateDeckListItem(deck);
            }
        }

        /// <summary>
        /// 创建新牌组按钮点击
        /// </summary>
        private void OnCreateNewDeckClicked()
        {
            string newDeckName = $"新牌组_{DateTime.Now:yyyyMMdd_HHmmss}";
            CardDeck newDeck = CardDeck.CreateNew(newDeckName);
            newDeck.Description = "在 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " 创建";

            // 添加到列表
            _decks.Add(newDeck);

            // 保存
            SaveDecksAsync().Forget();

            // 创建列表项
            CreateDeckListItem(newDeck);

            // 更新统计
            UpdateStatistics();

            UIManager.Instance.ShowNotification($"已创建新牌组: {newDeckName}");
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void OnRefreshClicked()
        {
            RefreshDeckList();
            UIManager.Instance.ShowNotification("牌组列表已刷新");
        }

        /// <summary>
        /// 编辑牌组按钮点击
        /// </summary>
        private void OnEditDeckClicked()
        {
            if (_selectedDeck == null) return;

            // 跳转到牌组构建器
            OnDeckEditRequested?.Invoke(_selectedDeck);
        }

        /// <summary>
        /// 删除牌组按钮点击
        /// </summary>
        private void OnDeleteDeckClicked()
        {
            if (_selectedDeck == null) return;

            // TODO: 显示确认对话框
            DeleteDeck(_selectedDeck);
        }

        /// <summary>
        /// 删除牌组
        /// </summary>
        private void DeleteDeck(CardDeck deck)
        {
            _decks.Remove(deck);

            // 从列表中移除
            foreach (var item in _deckItems)
            {
                if (item.Deck == deck)
                {
                    Destroy(item.gameObject);
                    _deckItems.Remove(item);
                    break;
                }
            }

            // 保存
            SaveDecksAsync().Forget();

            // 更新统计
            UpdateStatistics();

            // 重置选中
            _selectedDeck = null;
            _selectedDeckItem = null;
            SetDeckOperationButtonsEnabled(false);

            // 隐藏详情面板
            if (_deckDetailPanel != null)
            {
                _deckDetailPanel.SetActive(false);
            }

            UIManager.Instance.ShowNotification($"牌组 '{deck.DeckName}' 已删除");
        }

        /// <summary>
        /// 导出牌组按钮点击
        /// </summary>
        private void OnExportDeckClicked()
        {
            if (_selectedDeck == null) return;

            ExportDeckAsync(_selectedDeck).Forget();
        }

        /// <summary>
        /// 导出牌组异步
        /// </summary>
        private async UniTaskVoid ExportDeckAsync(CardDeck deck)
        {
            UIManager.Instance.ShowLoading(true);

            try
            {
                // 获取牌组中的卡牌数据
                var cards = deck.GetCards();

                // 创建导出数据
                var exportData = new DeckExportData
                {
                    Version = "1.0",
                    ExportDate = DateTime.Now.Ticks,
                    CardCount = cards.Count,
                    Cards = cards
                };

                // 转换为 JSON
                string json = JsonUtility.ToJson(exportData, true);

                // 确保目录存在
                string directory = System.IO.Path.Combine(
                    UnityEngine.Application.persistentDataPath,
                    "Decks",
                    "Export"
                );
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // 保存到文件
                string filePath = System.IO.Path.Combine(directory, $"{deck.DeckName}.json");
                System.IO.File.WriteAllText(filePath, json);

                UIManager.Instance.ShowLoading(false);
                UIManager.Instance.ShowNotification($"牌组已导出到: {filePath}");
            }
            catch (Exception e)
            {
                UIManager.Instance.ShowLoading(false);
                UIManager.Instance.ShowNotification($"导出失败: {e.Message}");
            }

            await UniTask.Yield();
        }

        /// <summary>
        /// 导入牌组按钮点击
        /// </summary>
        private void OnImportDeckClicked()
        {
            // TODO: 显示文件选择对话框
            ImportDeckAsync().Forget();
        }

        /// <summary>
        /// 导入牌组异步
        /// </summary>
        private async UniTaskVoid ImportDeckAsync()
        {
            UIManager.Instance.ShowNotification("请选择要导入的牌组文件");

            // TODO: 实现文件选择对话框
            // 这里需要使用不同平台的文件选择方式

            await UniTask.Yield();
        }

        /// <summary>
        /// 关闭详情按钮点击
        /// </summary>
        private void OnCloseDetailClicked()
        {
            if (_deckDetailPanel != null)
            {
                _deckDetailPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 保存牌组异步
        /// </summary>
        private async UniTaskVoid SaveDecksAsync()
        {
            try
            {
                var wrapper = new DeckListWrapper
                {
                    Version = "1.0",
                    LastModified = DateTime.Now.Ticks,
                    Decks = _decks
                };

                string json = JsonUtility.ToJson(wrapper, true);

                // 确保目录存在
                string directory = System.IO.Path.Combine(
                    UnityEngine.Application.persistentDataPath,
                    "Decks"
                );
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // 保存到文件
                string filePath = System.IO.Path.Combine(directory, "decks.json");
                System.IO.File.WriteAllText(filePath, json);

                Debug.Log($"牌组已保存: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"保存牌组失败: {e.Message}");
            }

            await UniTask.Yield();
        }

        /// <summary>
        /// 设置选中的牌组
        /// </summary>
        public void SetSelectedDeck(DeckListItemUI deckItem)
        {
            // 取消之前的选择
            if (_selectedDeckItem != null)
            {
                _selectedDeckItem.SetSelected(false);
            }

            // 设置新选择
            _selectedDeckItem = deckItem;
            _selectedDeck = deckItem?.Deck;

            if (_selectedDeckItem != null)
            {
                _selectedDeckItem.SetSelected(true);
                ShowDeckDetail(_selectedDeck);
                SetDeckOperationButtonsEnabled(true);
            }
            else
            {
                SetDeckOperationButtonsEnabled(false);
            }

            // 触发事件
            OnDeckSelected?.Invoke(_selectedDeck);
        }

        protected override void OnShow()
        {
            base.OnShow();
            RefreshDeckList();
        }
    }

    /// <summary>
    /// 牌组列表项 UI
    /// </summary>
    public class DeckListItemUI : MonoBehaviour
    {
        [Header("牌组信息显示")]
        [SerializeField] private Image _deckBackground;
        [SerializeField] private TextMeshProUGUI _deckNameText;
        [SerializeField] private TextMeshProUGUI _deckCardCountText;
        [SerializeField] private TextMeshProUGUI _deckModifiedTimeText;
        [SerializeField] private GameObject _selectedIndicator;

        [Header("颜色配置")]
        [SerializeField] private Color _selectedColor = new Color(0.5f, 0.8f, 1f);
        [SerializeField] private Color _normalColor = new Color(0.9f, 0.9f, 0.9f);

        private CardDeck _deck;
        private DeckLibraryUI _parentLibrary;

        public CardDeck Deck => _deck;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(CardDeck deck, DeckLibraryUI parentLibrary)
        {
            _deck = deck;
            _parentLibrary = parentLibrary;

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
            if (_deck == null) return;

            // 牌组名称
            if (_deckNameText != null)
            {
                _deckNameText.text = _deck.DeckName ?? "未命名牌组";
            }

            // 卡牌数量
            if (_deckCardCountText != null)
            {
                _deckCardCountText.text = $"卡牌数: {_deck.CardCount}";
            }

            // 修改时间
            if (_deckModifiedTimeText != null)
            {
                _deckModifiedTimeText.text = _deck.ModifiedTime.ToString("yyyy-MM-dd HH:mm");
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

            if (_deckBackground != null)
            {
                _deckBackground.color = selected ? _selectedColor : _normalColor;
            }
        }

        /// <summary>
        /// 列表项点击
        /// </summary>
        private void OnItemClicked()
        {
            _parentLibrary?.SetSelectedDeck(this);
        }
    }

    /// <summary>
    /// 牌组列表包装类
    /// </summary>
    [Serializable]
    public class DeckListWrapper
    {
        public string Version;
        public long LastModified;
        public List<CardDeck> Decks;
    }
}
