using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CardCore.UI
{
    /// <summary>
    /// 基础 UI 面板类 - 所有 UI 面板的基类
    /// </summary>
    public abstract class BaseUI : MonoBehaviour
    {
        [Header("面板设置")]
        [SerializeField] protected bool _useAnimation = true;
        [SerializeField] protected float _animationDuration = 0.3f;
        
        protected CanvasGroup _canvasGroup;
        protected bool _isInitialized = false;
        protected bool _isVisible = false;

        /// <summary>
        /// 面板是否可见
        /// </summary>
        public bool IsVisible => _isVisible;

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化面板
        /// </summary>
        protected virtual void Initialize()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public virtual void Show()
        {
            if (!_isInitialized) Initialize();

            gameObject.SetActive(true);
            _isVisible = true;

            if (_useAnimation)
            {
                ShowAnimationAsync().Forget();
            }
            else
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1;
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                }
            }

            OnShow();
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public virtual void Hide()
        {
            if (!_isInitialized) Initialize();

            _isVisible = false;

            if (_useAnimation)
            {
                HideAnimationAsync().Forget();
            }
            else
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }
                gameObject.SetActive(false);
            }

            OnHide();
        }

        /// <summary>
        /// 显示面板动画异步
        /// </summary>
        protected virtual async UniTaskVoid ShowAnimationAsync()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;

                float elapsedTime = 0f;
                while (elapsedTime < _animationDuration)
                {
                    _canvasGroup.alpha = elapsedTime / _animationDuration;
                    elapsedTime += Time.deltaTime;
                    await UniTask.Yield();
                }

                _canvasGroup.alpha = 1;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            await UniTask.Yield();
        }

        /// <summary>
        /// 隐藏面板动画异步
        /// </summary>
        protected virtual async UniTaskVoid HideAnimationAsync()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;

                float elapsedTime = 0f;
                while (elapsedTime < _animationDuration)
                {
                    _canvasGroup.alpha = 1 - (elapsedTime / _animationDuration);
                    elapsedTime += Time.deltaTime;
                    await UniTask.Yield();
                }

                _canvasGroup.alpha = 0;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 面板显示时调用
        /// </summary>
        protected virtual void OnShow()
        {
        }

        /// <summary>
        /// 面板隐藏时调用
        /// </summary>
        protected virtual void OnHide()
        {
        }

        /// <summary>
        /// 刷新面板数据
        /// </summary>
        public virtual void Refresh()
        {
        }

        /// <summary>
        /// 重置面板状态
        /// </summary>
        public virtual void Reset()
        {
        }
    }
}
