using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// MonoBehaviour单例模式基类
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static bool _applicationQuitting = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationQuitting)
                {
                    Debug.LogWarning($"[{typeof(T)}] 应用程序正在退出，不再创建新实例。");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 在场景中查找现有实例
                        _instance = FindAnyObjectByType<T>();

                        if (_instance == null)
                        {
                            // 创建新的GameObject并添加组件
                            var singletonObject = new GameObject();
                            _instance = singletonObject.AddComponent<T>();
                            singletonObject.name = $"{typeof(T).Name} (Singleton)";

                            Debug.Log($"[{typeof(T)}] 创建新的单例实例");
                        }
                        else
                        {
                            Debug.Log($"[{typeof(T)}] 使用场景中的现有实例");
                        }
                    }

                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = this as T;

                    // 确保单例在场景切换时不会被销毁
                    if (transform.parent == null)
                    {
                        DontDestroyOnLoad(gameObject);
                    }

                    Debug.Log($"[{typeof(T)}] 单例实例已注册");
                }
                else if (_instance != this)
                {
                    // 发现重复实例，销毁新创建的实例
                    Debug.LogWarning($"[{typeof(T)}] 检测到重复实例，销毁此实例。");
                    Destroy(gameObject);
                }
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationQuitting = true;
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
            }
            _instance = null;
        }
    }
}
