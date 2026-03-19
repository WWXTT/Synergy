using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件数据基接口
/// 事件分为广播事件和定向事件
/// 广播事件会触发所有订阅者
/// 定向事件在触发时需要额外指定接收者ID，ID通常是创建多个子类时子类在List中的索引
/// </summary>
/// 警告！ 
/// 代码大多数事件是用于数据层沟通UI层的，UI操作只能在主线程中执行，不允许在异步中使用UI事件
/// 新增！
/// Func<T, bool> 返回事件是否执行 事件自身有自己的规则校验，没有通过则发送者需要回卷//尚未实现回卷
public interface IEventData { }

public interface ITargetedEventData : IEventData
{
    // 通常是父物体创建子类时分配的索引ID
    int TargetId { get; }
}


// 事件处理器接口
public interface IEventHandler<T> where T : IEventData { }

// 扩展方法支持接收者标识
public static class EventHandlerExtensions
{
    public static void RegisterHandler<T>(
        this IEventHandler<T> handler,
        Func<T, bool> handlerAction,
        int receiverId = -1) where T : IEventData
    {
        EventManager.Instance.Subscribe(handlerAction, receiverId);
    }

    public static void UnregisterHandler<T>(
        this IEventHandler<T> handler,
        Func<T, bool> handlerAction,
        int receiverId = -1) where T : IEventData
    {
        EventManager.Instance.Unsubscribe(handlerAction, receiverId);
    }
}



// 事件触发器接口
public interface IEventTrigger { }

// 扩展方法支持定向触发
public static class EventTriggerExtensions
{
    public static bool TriggerEvent<T>(this IEventTrigger eventTrigger, T eventData, int receiverId = -1)
        where T : IEventData
    {
        return EventManager.Instance.Publish(eventData, receiverId);
    }
}

public sealed class EventManager
{
    private static EventManager _instance;
    public static EventManager Instance => _instance ??= new EventManager();

    // 广播事件处理程序 
    private readonly Dictionary<Type, List<Func<IEventData, bool>>> _broadcastHandlers =
         new Dictionary<Type, List<Func<IEventData, bool>>>(64);

    // 定向事件处理程序（指定接收者ID）
    private readonly Dictionary<Type, Dictionary<int, List<Func<IEventData, bool>>>> _targetedHandlers =
        new Dictionary<Type, Dictionary<int, List<Func<IEventData, bool>>>>(64);

    // 对象池 
    private readonly Stack<List<Func<IEventData, bool>>> _listPool = new Stack<List<Func<IEventData, bool>>>();

    public void Subscribe<T>(Func<T, bool> handler, int receiverId) where T : IEventData
    {
        var eventType = typeof(T);

        if (receiverId == -1)
        {
            if (!_broadcastHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Func<IEventData, bool>>(4);
                _broadcastHandlers[eventType] = handlers;
            }
            handlers.Add(evt => handler((T)evt));
        }
        else
        {
            if (!_targetedHandlers.TryGetValue(eventType, out var idToHandlers))
            {
                idToHandlers = new Dictionary<int, List<Func<IEventData, bool>>>();
                _targetedHandlers[eventType] = idToHandlers;
            }
            if (!idToHandlers.TryGetValue(receiverId, out var handlers))
            {
                handlers = new List<Func<IEventData, bool>>(4);
                idToHandlers[receiverId] = handlers;
            }
            handlers.Add(evt => handler((T)evt));
        }
    }

    public void Unsubscribe<T>(Func<T, bool> handler, int receiverId) where T : IEventData
    {
        var eventType = typeof(T);

        if (receiverId == -1)
        {
            if (_broadcastHandlers.TryGetValue(eventType, out var handlers))
            {
                for (int i = handlers.Count - 1; i >= 0; i--)
                {
                    if (handlers[i].Target == handler.Target)
                    {
                        handlers.RemoveAt(i);
                        break;
                    }
                }
                if (handlers.Count == 0) _broadcastHandlers.Remove(eventType);
            }
        }
        else
        {
            if (_targetedHandlers.TryGetValue(eventType, out var idToHandlers))
            {
                if (idToHandlers.TryGetValue(receiverId, out var handlers))
                {
                    for (int i = handlers.Count - 1; i >= 0; i--)
                    {
                        if (handlers[i].Target == handler.Target)
                        {
                            handlers.RemoveAt(i);
                            break;
                        }
                    }
                    if (handlers.Count == 0)
                    {
                        idToHandlers.Remove(receiverId);
                        if (idToHandlers.Count == 0)
                            _targetedHandlers.Remove(eventType);
                    }
                }
            }
        }
    }

    public bool Publish<T>(T eventData, int targetId) where T : IEventData
    {
        var eventType = typeof(T);
        List<Func<IEventData, bool>> handlersToInvoke = GetHandlerListFromPool();
        bool allSuccess = true;

        try
        {
            if (targetId == -1) // 广播事件
            {
                if (_broadcastHandlers.TryGetValue(eventType, out var handlers))
                {
                    handlersToInvoke.AddRange(handlers);
                }

                // 没有处理器时返回 false
                if (handlersToInvoke.Count == 0) return false;

                foreach (var handler in handlersToInvoke)
                {
                    try
                    {
                        if (!IsHandlerValid(handler)) continue;
                        //广播时即时其中一个没有执行也继续调用下一个 但是最终返回值有一个没有执行就会返回false
                        if (!handler(eventData))
                        {
                            allSuccess = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Event handler error: {e}");
                        allSuccess = false;
                    }
                }
                return allSuccess;
            }
            else // 定向事件
            {
                if (_targetedHandlers.TryGetValue(eventType, out var idToHandlers) &&
                    idToHandlers.TryGetValue(targetId, out var handlers))
                {
                    handlersToInvoke.AddRange(handlers);
                }

                // 没有处理器时返回 false
                if (handlersToInvoke.Count == 0) return false;

                foreach (var handler in handlersToInvoke)
                {
                    try
                    {
                        if (!IsHandlerValid(handler)) continue;
                        if (!handler(eventData))
                        {
                            return false; // handler自己的事件规则校验不通过 拒绝执行
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Event handler error: {e}");
                        return false;// 其他原因不通过 没有执行
                    }
                }
                return true;
            }
        }
        finally
        {
            handlersToInvoke.Clear();
            // 回收对象池
            if (_listPool.Count < 16)
            {
                _listPool.Push(handlersToInvoke);
            }
        }
    }

    // 检查处理器是否有效
    private bool IsHandlerValid(Func<IEventData, bool> handler)
    {
        return !(handler.Target is UnityEngine.Object obj) || obj;
    }

    // 对象池
    private List<Func<IEventData, bool>> GetHandlerListFromPool()
    {
        return _listPool.Count > 0 ? _listPool.Pop() : new List<Func<IEventData, bool>>(16);
    }

    // 清理无效的处理器
    public void CleanupInvalidHandlers()
    {
        CleanupBroadcastHandlers();
        CleanupTargetedHandlers();
    }

    private void CleanupBroadcastHandlers()
    {
        var typesToRemove = new List<Type>();

        foreach (var kvp in _broadcastHandlers)
        {
            var handlers = kvp.Value;
            handlers.RemoveAll(handler => !IsHandlerValid(handler));

            if (handlers.Count == 0)
            {
                typesToRemove.Add(kvp.Key);
            }
        }

        foreach (var type in typesToRemove)
        {
            _broadcastHandlers.Remove(type);
        }
    }

    private void CleanupTargetedHandlers()
    {
        var typesToRemove = new List<Type>();

        foreach (var typeKvp in _targetedHandlers)
        {
            var idToHandlers = typeKvp.Value;
            var idsToRemove = new List<int>();

            foreach (var idKvp in idToHandlers)
            {
                var handlers = idKvp.Value;
                handlers.RemoveAll(handler => !IsHandlerValid(handler));

                if (handlers.Count == 0)
                {
                    idsToRemove.Add(idKvp.Key);
                }
            }

            foreach (var id in idsToRemove)
            {
                idToHandlers.Remove(id);
            }

            if (idToHandlers.Count == 0)
            {
                typesToRemove.Add(typeKvp.Key);
            }
        }

        foreach (var type in typesToRemove)
        {
            _targetedHandlers.Remove(type);
        }
    }
}