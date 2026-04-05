using System;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace CardCore.UI.UIToolkit
{
    /// <summary>
    /// UI Toolkit 面板基类
    /// 所有 UI Toolkit 面板的基类，替代 UGUI 的 BaseUI
    /// 使用 VisualElement 进行显示/隐藏动画
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class BaseUIToolkitPanel : MonoBehaviour
    {
        [Header("面板设置")]
        [SerializeField] protected bool _useAnimation = true;
        [SerializeField] protected float _animationDuration = 0.3f;
        [SerializeField] protected string _panelName;

        protected UIDocument _uiDocument;
        protected VisualElement _rootElement;
        protected VisualElement _panelContainer;
        protected bool _isInitialized = false;
        protected bool _isVisible = false;

        /// <summary>
        /// 面板名称
        /// </summary>
        public string PanelName => _panelName;

        /// <summary>
        /// 面板是否可见
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// 根元素
        /// </summary>
        public VisualElement RootElement => _rootElement;

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化面板
        /// </summary>
        protected virtual void Initialize()
        {
            if (_isInitialized) return;

            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                Debug.LogError($"[{GetType().Name}] UIDocument component not found!");
                return;
            }

            _rootElement = _uiDocument.rootVisualElement;
            if (_rootElement == null)
            {
                Debug.LogError($"[{GetType().Name}] Root VisualElement not found!");
                return;
            }

            // 查找面板容器（通常名为 panel-container 或与面板名相同）
            _panelContainer = _rootElement.Q<VisualElement>("panel-container") ??
                              _rootElement.Q<VisualElement>(_panelName) ??
                              _rootElement;

            // 绑定UI元素
            BindUIElements();

            // 注册事件
            RegisterEvents();

            _isInitialized = true;

            // 初始状态为隐藏
            if (_panelContainer != null)
            {
                _panelContainer.style.display = DisplayStyle.None;
                _panelContainer.style.opacity = 0;
            }
        }

        /// <summary>
        /// 绑定UI元素 - 子类实现
        /// </summary>
        protected virtual void BindUIElements()
        {
        }

        /// <summary>
        /// 注册事件 - 子类实现
        /// </summary>
        protected virtual void RegisterEvents()
        {
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public virtual void Show()
        {
            if (!_isInitialized) Initialize();

            if (_panelContainer == null) return;

            _isVisible = true;
            _panelContainer.style.display = DisplayStyle.Flex;

            if (_useAnimation)
            {
                ShowAnimationAsync().Forget();
            }
            else
            {
                _panelContainer.style.opacity = 1;
                SetInteractable(true);
            }

            OnShow();
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public virtual void Hide()
        {
            if (!_isInitialized) Initialize();

            if (_panelContainer == null) return;

            _isVisible = false;

            if (_useAnimation)
            {
                HideAnimationAsync().Forget();
            }
            else
            {
                _panelContainer.style.opacity = 0;
                _panelContainer.style.display = DisplayStyle.None;
            }

            OnHide();
        }

        /// <summary>
        /// 切换面板显示状态
        /// </summary>
        public virtual void Toggle()
        {
            if (_isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>
        /// 显示面板动画异步
        /// </summary>
        protected virtual async UniTaskVoid ShowAnimationAsync()
        {
            if (_panelContainer == null) return;

            SetInteractable(false);

            float elapsed = 0f;
            float startOpacity = 0f;
            float targetOpacity = 1f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / _animationDuration;
                float easedProgress = EaseOutCubic(progress);
                _panelContainer.style.opacity = Mathf.Lerp(startOpacity, targetOpacity, easedProgress);
                await UniTask.Yield();
            }

            _panelContainer.style.opacity = targetOpacity;
            SetInteractable(true);
        }

        /// <summary>
        /// 隐藏面板动画异步
        /// </summary>
        protected virtual async UniTaskVoid HideAnimationAsync()
        {
            if (_panelContainer == null) return;

            SetInteractable(false);

            float elapsed = 0f;
            float startOpacity = 1f;
            float targetOpacity = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / _animationDuration;
                float easedProgress = EaseInCubic(progress);
                _panelContainer.style.opacity = Mathf.Lerp(startOpacity, targetOpacity, easedProgress);
                await UniTask.Yield();
            }

            _panelContainer.style.opacity = targetOpacity;
            _panelContainer.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// 设置交互状态
        /// </summary>
        protected virtual void SetInteractable(bool interactable)
        {
            if (_panelContainer == null) return;

            _panelContainer.pickingMode = interactable ? PickingMode.Position : PickingMode.Ignore;
        }

        /// <summary>
        /// 缓动函数 - 缓出三次方
        /// </summary>
        protected float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        /// <summary>
        /// 缓动函数 - 缓入三次方
        /// </summary>
        protected float EaseInCubic(float t)
        {
            return t * t * t;
        }

        /// <summary>
        /// 面板显示时调用 - 子类可重写
        /// </summary>
        protected virtual void OnShow()
        {
        }

        /// <summary>
        /// 面板隐藏时调用 - 子类可重写
        /// </summary>
        protected virtual void OnHide()
        {
        }

        /// <summary>
        /// 刷新面板数据 - 子类实现
        /// </summary>
        public virtual void Refresh()
        {
        }

        /// <summary>
        /// 重置面板状态 - 子类实现
        /// </summary>
        public virtual void ResetPanel()
        {
        }

        #region 辅助方法

        /// <summary>
        /// 获取元素
        /// </summary>
        protected T Q<T>(string name) where T : VisualElement
        {
            if (_rootElement == null) return null;
            return _rootElement.Q<T>(name);
        }

        /// <summary>
        /// 获取元素
        /// </summary>
        protected VisualElement Q(string name)
        {
            if (_rootElement == null) return null;
            return _rootElement.Q(name);
        }

        /// <summary>
        /// 绑定按钮点击事件
        /// </summary>
        protected void BindButton(string buttonName, Action callback)
        {
            var button = Q<Button>(buttonName);
            if (button != null)
            {
                button.clickable = null;
                button.RegisterCallback<ClickEvent>(evt => callback?.Invoke());
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Button '{buttonName}' not found!");
            }
        }

        /// <summary>
        /// 绑定输入框值变化事件
        /// </summary>
        protected void BindTextField(string fieldName, Action<string> callback)
        {
            var field = Q<TextField>(fieldName);
            if (field != null)
            {
                field.RegisterValueChangedCallback(evt => callback?.Invoke(evt.newValue));
            }
        }

        /// <summary>
        /// 绑定下拉框值变化事件
        /// </summary>
        protected void BindDropdownField(string fieldName, Action<string> callback)
        {
            var field = Q<DropdownField>(fieldName);
            if (field != null)
            {
                field.RegisterValueChangedCallback(evt => callback?.Invoke(evt.newValue));
            }
        }

        /// <summary>
        /// 绑定开关值变化事件
        /// </summary>
        protected void BindToggle(string toggleName, Action<bool> callback)
        {
            var toggle = Q<Toggle>(toggleName);
            if (toggle != null)
            {
                toggle.RegisterValueChangedCallback(evt => callback?.Invoke(evt.newValue));
            }
        }

        /// <summary>
        /// 绑定滑动条值变化事件
        /// </summary>
        protected void BindSlider(string sliderName, Action<float> callback)
        {
            var slider = Q<Slider>(sliderName);
            if (slider != null)
            {
                slider.RegisterValueChangedCallback(evt => callback?.Invoke(evt.newValue));
            }
        }

        /// <summary>
        /// 设置元素显示状态
        /// </summary>
        protected void SetElementVisible(string elementName, bool visible)
        {
            var element = Q(elementName);
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// 设置元素文本
        /// </summary>
        protected void SetLabelText(string labelName, string text)
        {
            var label = Q<Label>(labelName);
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>
        /// 设置输入框值
        /// </summary>
        protected void SetTextFieldValue(string fieldName, string value)
        {
            var field = Q<TextField>(fieldName);
            if (field != null)
            {
                field.value = value;
            }
        }

        #endregion
    }
}