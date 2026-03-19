using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using CardCore.UI.UIToolkit;

namespace CardCore.UI
{
    /// <summary>
    /// UI 管理器 - 管理所有 UI 面板的显示和切换
    /// 支持UGUI和UI Toolkit两种UI系统
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        [Header("UGUI 面板引用")]
        [SerializeField] private GameObject _cardEditorPanel;
        [SerializeField] private GameObject _deckLibraryPanel;
        [SerializeField] private GameObject _deckBuilderPanel;
        [SerializeField] private GameObject _cardListPanel;
        [SerializeField] private GameObject _effectBuilderPanel;

        [Header("UI Toolkit 面板引用")]
        [SerializeField] private UIDocument _mainUIDocument;
        [SerializeField] private CardEditorPanel _cardEditorUITK;
        [SerializeField] private EffectEditorPanel _effectEditorUITK;
        [SerializeField] private DeckEditorPanel _deckEditorUITK;

        [Header("全局 UI 引用")]
        [SerializeField] private GameObject _loadingPanel;
        [SerializeField] private GameObject _notificationPanel;

        // 当前显示的面板 (UGUI)
        private GameObject _currentPanel;

        // 当前显示的UI Toolkit面板
        private BaseUIToolkitPanel _currentUITKPanel;

        // UGUI面板字典
        private Dictionary<string, GameObject> _panels = new Dictionary<string, GameObject>();

        // UI Toolkit面板字典
        private Dictionary<string, BaseUIToolkitPanel> _uitkPanels = new Dictionary<string, BaseUIToolkitPanel>();

        // 场景名称常量
        public const string CARD_EDITOR_SCENE = "CardEditor";
        public const string DECK_LIBRARY_SCENE = "DeckLibrary";
        public const string DECK_BUILDER_SCENE = "DeckBuilder";

        // 面板名称常量
        public const string PANEL_CARD_EDITOR = "CardEditor";
        public const string PANEL_EFFECT_EDITOR = "EffectEditor";
        public const string PANEL_DECK_EDITOR = "DeckEditor";

        protected override void Awake()
        {
            DontDestroyOnLoad(this);
            base.Awake();

            // 初始化UGUI面板字典
            if (_cardEditorPanel != null) _panels[_cardEditorPanel.name] = _cardEditorPanel;
            if (_deckLibraryPanel != null) _panels[_deckLibraryPanel.name] = _deckLibraryPanel;
            if (_deckBuilderPanel != null) _panels[_deckBuilderPanel.name] = _deckBuilderPanel;
            if (_cardListPanel != null) _panels[_cardListPanel.name] = _cardListPanel;
            if (_effectBuilderPanel != null) _panels[_effectBuilderPanel.name] = _effectBuilderPanel;

            // 初始化UI Toolkit面板字典
            if (_cardEditorUITK != null) _uitkPanels[PANEL_CARD_EDITOR] = _cardEditorUITK;
            if (_effectEditorUITK != null) _uitkPanels[PANEL_EFFECT_EDITOR] = _effectEditorUITK;
            if (_deckEditorUITK != null) _uitkPanels[PANEL_DECK_EDITOR] = _deckEditorUITK;

            // 隐藏所有面板
            HideAllPanels();
        }

        private void Start()
        {
            // 加载卡牌注册表
            LoadCardRegistryAsync().Forget();
        }

        /// <summary>
        /// 异步加载卡牌注册表
        /// </summary>
        private async UniTaskVoid LoadCardRegistryAsync()
        {
            ShowLoading(true);

            // 等待注册表加载完成
            if (!CardDataRegistry.Instance.IsLoaded)
            {
                await CardDataRegistry.Instance.LoadAsync();
            }

            ShowLoading(false);
            Debug.Log($"UIManager: 加载了 {CardDataRegistry.Instance.Count} 张卡牌");
        }

        /// <summary>
        /// 隐藏所有面板
        /// </summary>
        public void HideAllPanels()
        {
            // 隐藏UGUI面板
            foreach (var kvp in _panels)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.SetActive(false);
                }
            }
            _currentPanel = null;

            // 隐藏UI Toolkit面板
            foreach (var kvp in _uitkPanels)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Hide();
                }
            }
            _currentUITKPanel = null;
        }

        #region UGUI 面板管理

        /// <summary>
        /// 显示指定UGUI面板
        /// </summary>
        public void ShowPanel(string panelName)
        {
            if (_panels.TryGetValue(panelName, out var panel))
            {
                // 隐藏当前面板
                if (_currentPanel != null && _currentPanel != panel)
                {
                    _currentPanel.SetActive(false);
                }

                // 显示新面板
                panel.SetActive(true);
                _currentPanel = panel;

                Debug.Log($"UIManager: 显示UGUI面板 {panelName}");
            }
            else
            {
                Debug.LogWarning($"UIManager: 未找到UGUI面板 {panelName}");
            }
        }

        /// <summary>
        /// 隐藏指定UGUI面板
        /// </summary>
        public void HidePanel(string panelName)
        {
            if (_panels.TryGetValue(panelName, out var panel))
            {
                panel.SetActive(false);

                if (_currentPanel == panel)
                {
                    _currentPanel = null;
                }

                Debug.Log($"UIManager: 隐藏UGUI面板 {panelName}");
            }
        }

        /// <summary>
        /// 获取指定UGUI面板
        /// </summary>
        public GameObject GetPanel(string panelName)
        {
            if (_panels.TryGetValue(panelName, out var panel))
            {
                return panel;
            }
            return null;
        }

        /// <summary>
        /// 检查指定UGUI面板是否正在显示
        /// </summary>
        public bool IsPanelVisible(string panelName)
        {
            if (_panels.TryGetValue(panelName, out var panel))
            {
                return panel.activeSelf;
            }
            return false;
        }

        /// <summary>
        /// 获取当前显示的UGUI面板
        /// </summary>
        public GameObject GetCurrentPanel() => _currentPanel;

        #endregion

        #region UI Toolkit 面板管理

        /// <summary>
        /// 显示UI Toolkit面板
        /// </summary>
        public void ShowUITKPanel(string panelName)
        {
            if (_uitkPanels.TryGetValue(panelName, out var panel))
            {
                // 隐藏当前面板
                if (_currentUITKPanel != null && _currentUITKPanel != panel)
                {
                    _currentUITKPanel.Hide();
                }

                // 显示新面板
                panel.Show();
                _currentUITKPanel = panel;

                Debug.Log($"UIManager: 显示UI Toolkit面板 {panelName}");
            }
            else
            {
                Debug.LogWarning($"UIManager: 未找到UI Toolkit面板 {panelName}");
            }
        }

        /// <summary>
        /// 隐藏UI Toolkit面板
        /// </summary>
        public void HideUITKPanel(string panelName)
        {
            if (_uitkPanels.TryGetValue(panelName, out var panel))
            {
                panel.Hide();

                if (_currentUITKPanel == panel)
                {
                    _currentUITKPanel = null;
                }

                Debug.Log($"UIManager: 隐藏UI Toolkit面板 {panelName}");
            }
        }

        /// <summary>
        /// 获取UI Toolkit面板
        /// </summary>
        public T GetUITKPanel<T>(string panelName) where T : BaseUIToolkitPanel
        {
            if (_uitkPanels.TryGetValue(panelName, out var panel))
            {
                return panel as T;
            }
            return null;
        }

        /// <summary>
        /// 检查UI Toolkit面板是否正在显示
        /// </summary>
        public bool IsUITKPanelVisible(string panelName)
        {
            if (_uitkPanels.TryGetValue(panelName, out var panel))
            {
                return panel.IsVisible;
            }
            return false;
        }

        /// <summary>
        /// 获取当前显示的UI Toolkit面板
        /// </summary>
        public BaseUIToolkitPanel GetCurrentUITKPanel() => _currentUITKPanel;

        /// <summary>
        /// 刷新当前UI Toolkit面板
        /// </summary>
        public void RefreshCurrentUITKPanel()
        {
            _currentUITKPanel?.Refresh();
        }

        #endregion

        #region 全局UI

        /// <summary>
        /// 显示/隐藏加载面板
        /// </summary>
        public void ShowLoading(bool show)
        {
            if (_loadingPanel != null)
            {
                _loadingPanel.SetActive(show);
            }
        }

        /// <summary>
        /// 显示通知消息
        /// </summary>
        public void ShowNotification(string message, float duration = 2f)
        {
            ShowNotificationAsync(message, duration).Forget();
        }

        /// <summary>
        /// 显示通知消息异步
        /// </summary>
        private async UniTaskVoid ShowNotificationAsync(string message, float duration)
        {
            if (_notificationPanel != null)
            {
                _notificationPanel.SetActive(true);

                // TODO: 显示消息内容
                Debug.Log($"通知: {message}");

                await UniTask.Delay(System.TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());

                _notificationPanel.SetActive(false);
            }
        }

        #endregion

        #region 场景管理

        /// <summary>
        /// 加载场景
        /// </summary>
        public void LoadScene(string sceneName)
        {
            LoadSceneAsync(sceneName).Forget();
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        private async UniTaskVoid LoadSceneAsync(string sceneName)
        {
            ShowLoading(true);

            await SceneManager.LoadSceneAsync(sceneName);

            ShowLoading(false);
        }

        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion
    }
}