using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 游戏事件总线
    /// 连接各引擎和事件系统，实现统一的事件发布和订阅机制
    /// </summary>
    public static class GameEventBus
    {
        private static readonly List<IGameEventObserver> _observers =
            new List<IGameEventObserver>();

        /// <summary>
        /// 订阅游戏事件
        /// </summary>
        public static void Subscribe(IGameEventObserver observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }

        /// <summary>
        /// 取消订阅游戏事件
        /// </summary>
        public static void Unsubscribe(IGameEventObserver observer)
        {
            _observers.Remove(observer);
        }

        /// <summary>
        /// 发布游戏事件
        /// </summary>
        public static void Publish<T>(T e) where T : IGameEvent
        {
            foreach (var observer in _observers)
            {
                observer.OnGameEvent(e);
            }
        }

        /// <summary>
        /// 清空所有订阅者
        /// </summary>
        public static void ClearAll()
        {
            _observers.Clear();
        }

        /// <summary>
        /// 获取当前订阅者数量
        /// </summary>
        public static int ObserverCount => _observers.Count;
    }

    /// <summary>
    /// 游戏事件观察者接口
    /// 所有需要接收游戏事件的类都应实现此接口
    /// </summary>
    public interface IGameEventObserver
    {
        /// <summary>
        /// 处理游戏事件
        /// </summary>
        void OnGameEvent<T>(T e) where T : IGameEvent;
    }
}
