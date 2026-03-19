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
    /// 卡牌预览 UI - 用于显示卡牌的预览效果
    /// </summary>
    public class CardPreviewUI : MonoBehaviour
    {
        [Header("卡牌视觉元素")]
        [SerializeField] private Image _cardFrame;
        [SerializeField] private Image _cardBackground;
        [SerializeField] private Image _cardIllustration;
        [SerializeField] private TextMeshProUGUI _cardNameText;
        [SerializeField] private TextMeshProUGUI _cardTypeText;
        [SerializeField] private GameObject _legendaryIndicator;
        [SerializeField] private Image _cardTypeIcon;

        [Header("点击放大")]
        [SerializeField] private Button _previewButton;
        [SerializeField] private GameObject _enlargedPreviewPanel;
        [SerializeField] private CardPreviewUI _enlargedPreview;
        [SerializeField] private Button _closeEnlargedButton;

        [Header("战斗属性")]
        [SerializeField] private GameObject _combatStatsPanel;
        [SerializeField] private TextMeshProUGUI _lifeText;
        [SerializeField] private TextMeshProUGUI _powerText;
        [SerializeField] private Image _lifeIcon;
        [SerializeField] private Image _powerIcon;

        [Header("法力消耗")]
        [SerializeField] private GameObject _manaCostPanel;
        [SerializeField] private GameObject _manaCostContainer;
        [SerializeField] private GameObject _manaCostIconPrefab;

        [Header("效果列表")]
        [SerializeField] private GameObject _effectsPanel;
        [SerializeField] private Transform _effectsContainer;
        [SerializeField] private GameObject _effectTextPrefab;
        [SerializeField] private ScrollRect _effectsScrollRect;

        [Header("颜色配置")]
        [SerializeField] private Color _monsterColor = new Color(0.8f, 0.8f, 0.6f);
        [SerializeField] private Color _legendColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color _magicColor = new Color(0.6f, 0.6f, 0.9f);
        [SerializeField] private Color _fieldColor = new Color(0.6f, 0.9f, 0.6f);

        [Header("法力颜色配置")]
        [SerializeField] private Color _grayManaColor = new Color(0.61f, 0.66f, 0.66f);
        [SerializeField] private Color _redManaColor = new Color(1f, 0.27f, 0.27f);
        [SerializeField] private Color _blueManaColor = new Color(0.27f, 0.53f, 1f);
        [SerializeField] private Color _greenManaColor = new Color(0.27f, 0.67f, 0.27f);
        [SerializeField] private Color _whiteManaColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color _blackManaColor = new Color(0.2f, 0.2f, 0.2f);

        // 当前显示的卡牌数据
        private CardData _currentCardData;
        private List<GameObject> _manaCostIcons = new List<GameObject>();
        private List<GameObject> _effectTextObjects = new List<GameObject>();

        private void Awake()
        {
            // 初始化
            ClearManaCostIcons();
            ClearEffectTexts();

            // 初始化点击放大功能
            InitializeEnlargeFeature();
        }

        /// <summary>
        /// 初始化点击放大���能
        /// </summary>
        private void InitializeEnlargeFeature()
        {
            if (_previewButton != null)
            {
                _previewButton.onClick.AddListener(OnPreviewButtonClicked);
            }

            if (_closeEnlargedButton != null)
            {
                _closeEnlargedButton.onClick.AddListener(OnCloseEnlargedButtonClicked);
            }

            // 默认隐藏放大预览
            if (_enlargedPreviewPanel != null)
            {
                _enlargedPreviewPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 预览按钮点击
        /// </summary>
        private void OnPreviewButtonClicked()
        {
            if (_enlargedPreviewPanel != null && _currentCardData != null)
            {
                _enlargedPreviewPanel.SetActive(true);

                if (_enlargedPreview != null)
                {
                    _enlargedPreview.SetCardData(_currentCardData);
                }
            }
        }

        /// <summary>
        /// 关闭放大预览按钮点击
        /// </summary>
        private void OnCloseEnlargedButtonClicked()
        {
            if (_enlargedPreviewPanel != null)
            {
                _enlargedPreviewPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 设置卡牌数据
        /// </summary>
        public void SetCardData(CardData cardData)
        {
            _currentCardData = cardData;
            UpdatePreview();
        }

        /// <summary>
        /// 更新预览
        /// </summary>
        private void UpdatePreview()
        {
            if (_currentCardData == null) return;

            UpdateCardFrame();
            UpdateCardType();
            UpdateCardName();
            UpdateLegendaryIndicator();
            UpdateCombatStats();
            UpdateManaCost();
            UpdateEffects();
        }

        /// <summary>
        /// 更新卡牌边框
        /// </summary>
        private void UpdateCardFrame()
        {
            if (_cardBackground == null) return;

            Color frameColor = GetCardTypeColor(_currentCardData.CardType);
            _cardBackground.color = frameColor;
        }

        /// <summary>
        /// 获取卡牌类型颜色
        /// </summary>
        private Color GetCardTypeColor(CardType cardType)
        {
            switch (cardType)
            {
                case CardType.生物:
                    return _monsterColor;
                case CardType.传奇:
                    return _legendColor;
                case CardType.术法:
                    return _magicColor;
                case CardType.领域:
                    return _fieldColor;
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 更新卡牌类型显示
        /// </summary>
        private void UpdateCardType()
        {
            if (_cardTypeText != null)
            {
                _cardTypeText.text = GetCardTypeDisplayName(_currentCardData.CardType);
            }

            if (_cardTypeIcon != null)
            {
                // TODO: 设置卡牌类型图标
            }
        }

        /// <summary>
        /// 获取卡牌类型显示名称
        /// </summary>
        private string GetCardTypeDisplayName(CardType cardType)
        {
            switch (cardType)
            {
                case CardType.生物:
                    return "怪兽";
                case CardType.传奇:
                    return "传奇";
                case CardType.术法:
                    return "术法";
                case CardType.领域:
                    return "领域";
                default:
                    return cardType.ToString();
            }
        }

        /// <summary>
        /// 更新卡牌名称
        /// </summary>
        private void UpdateCardName()
        {
            if (_cardNameText != null)
            {
                _cardNameText.text = _currentCardData.CardName ?? string.Empty;
            }
        }

        /// <summary>
        /// 更新传奇指示器
        /// </summary>
        private void UpdateLegendaryIndicator()
        {
            if (_legendaryIndicator != null)
            {
                _legendaryIndicator.SetActive(_currentCardData.IsLegendary);
            }
        }

        /// <summary>
        /// 更新战斗属性
        /// </summary>
        private void UpdateCombatStats()
        {
            if (_combatStatsPanel == null) return;

            bool showStats = _currentCardData.CardType == CardType.生物 ||
                             _currentCardData.CardType == CardType.传奇;
            _combatStatsPanel.SetActive(showStats);

            if (!showStats) return;

            // 生命值
            if (_lifeText != null)
            {
                if (_currentCardData.Life.HasValue)
                {
                    _lifeText.text = _currentCardData.Life.Value.ToString();
                    _lifeText.gameObject.SetActive(true);
                    if (_lifeIcon != null) _lifeIcon.gameObject.SetActive(true);
                }
                else
                {
                    _lifeText.gameObject.SetActive(false);
                    if (_lifeIcon != null) _lifeIcon.gameObject.SetActive(false);
                }
            }

            // 攻击力
            if (_powerText != null)
            {
                if (_currentCardData.Power.HasValue)
                {
                    _powerText.text = _currentCardData.Power.Value.ToString();
                    _powerText.gameObject.SetActive(true);
                    if (_powerIcon != null) _powerIcon.gameObject.SetActive(true);
                }
                else
                {
                    _powerText.gameObject.SetActive(false);
                    if (_powerIcon != null) _powerIcon.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 更新法力消耗
        /// </summary>
        private void UpdateManaCost()
        {
            if (_manaCostPanel == null || _manaCostContainer == null) return;

            ClearManaCostIcons();

            bool hasCost = _currentCardData.Cost != null && _currentCardData.Cost.Count > 0;
            _manaCostPanel.SetActive(hasCost);

            if (!hasCost) return;

            // 创建法力消耗图标
            foreach (var kvp in _currentCardData.Cost)
            {
                if (kvp.Value <= 0) continue;

                ManaType manaType = (ManaType)kvp.Key;
                int count = Mathf.RoundToInt(kvp.Value);
                Color manaColor = GetManaTypeColor(manaType);

                for (int i = 0; i < count; i++)
                {
                    CreateManaCostIcon(manaColor);
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
        /// 创建法力消耗图标
        /// </summary>
        private void CreateManaCostIcon(Color color)
        {
            if (_manaCostIconPrefab == null || _manaCostContainer == null) return;

            GameObject iconObj = Instantiate(_manaCostIconPrefab);
            Image iconImage = iconObj.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.color = color;
            }

            _manaCostIcons.Add(iconObj);
        }

        /// <summary>
        /// 清除法力消耗图标
        /// </summary>
        private void ClearManaCostIcons()
        {
            foreach (var icon in _manaCostIcons)
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
            }
            _manaCostIcons.Clear();
        }

        /// <summary>
        /// 更新效果列表
        /// </summary>
        private void UpdateEffects()
        {
            if (_effectsPanel == null || _effectsContainer == null) return;

            ClearEffectTexts();

            bool hasEffects = _currentCardData.Effects != null && _currentCardData.Effects.Count > 0;
            _effectsPanel.SetActive(hasEffects);

            if (!hasEffects) return;

            // 创建效果文本
            foreach (var effect in _currentCardData.Effects)
            {
                if (string.IsNullOrWhiteSpace(effect.Description)) continue;

                GameObject textObj = Instantiate(_effectTextPrefab, _effectsContainer);
                TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();

                if (textMesh != null)
                {
                    // 格式化效果描述
                    string formattedEffect = FormatEffectDescription(effect);
                    textMesh.text = formattedEffect;
                }

                _effectTextObjects.Add(textObj);
            }

            // 自动滚动到顶部
            if (_effectsScrollRect != null)
            {
                _effectsScrollRect.normalizedPosition = Vector2.one;
            }
        }

        /// <summary>
        /// 格式化效果描述
        /// </summary>
        private string FormatEffectDescription(EffectData effect)
        {
            if (string.IsNullOrEmpty(effect.Description)) return string.Empty;

            string prefix = string.Empty;

            // 根据效果速度添加前缀
            switch (effect.Speed)
            {
                case EffectSpeed.强制诱发:
                    prefix = "[强制] ";
                    break;
                case EffectSpeed.可选诱发:
                    prefix = effect.Initiative ? "[主动] " : "[诱发] ";
                    break;
                case EffectSpeed.自由时点:
                    prefix = "[自由] ";
                    break;
            }

            return prefix + effect.Description;
        }

        /// <summary>
        /// 清除效果文本对象
        /// </summary>
        private void ClearEffectTexts()
        {
            foreach (var textObj in _effectTextObjects)
            {
                if (textObj != null)
                {
                    Destroy(textObj);
                }
            }
            _effectTextObjects.Clear();
        }

        private void OnDestroy()
        {
            ClearManaCostIcons();
            ClearEffectTexts();
        }
    }
}
