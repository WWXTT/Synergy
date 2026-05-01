using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 效果处理器注册表
    /// 管理所有原子效果处理器的注册和查找
    /// </summary>
    public static class EffectHandlerRegistry
    {
        private static readonly Dictionary<AtomicEffectType, IAtomicEffectHandler> _handlers
            = new Dictionary<AtomicEffectType, IAtomicEffectHandler>();

        /// <summary>
        /// 注册处理器
        /// </summary>
        public static void Register(IAtomicEffectHandler handler)
        {
            if (handler == null) return;

            _handlers[handler.EffectType] = handler;

            var config = AtomicEffectTable.GetByType(handler.EffectType);
       
        }

        /// <summary>
        /// 获取处理器
        /// </summary>
        public static IAtomicEffectHandler GetHandler(AtomicEffectType type)
        {
            return _handlers.TryGetValue(type, out var handler) ? handler : null;
        }

        /// <summary>
        /// 尝试获取处理器
        /// </summary>
        public static bool TryGetHandler(AtomicEffectType type, out IAtomicEffectHandler handler)
        {
            return _handlers.TryGetValue(type, out handler);
        }

        /// <summary>
        /// 执行原子效果
        /// 每个原子效果根据自身的 AtomicEffectConfig 独立解析目标
        /// </summary>
        public static bool ExecuteEffect(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect == null || context == null) return false;

            if (!TryGetHandler(effect.Type, out var handler))
            {
                UnityEngine.Debug.LogWarning($"未注册的效果处理器: {effect.Type}");
                return false;
            }

            // 根据原子效果的配置解析目标
            var config = AtomicEffectTable.GetByType(effect.Type);
            if (config != null && config.TargetType != EffectTargetType.None)
            {
                if (config.TargetType == EffectTargetType.Self)
                {
                    context.Targets = new List<Entity> { context.Source };
                }
                else
                {
                    var resolver = new TargetResolver(context.ZoneManager);
                    var candidates = resolver.GetCandidates(config, context);
                    if (!string.IsNullOrEmpty(config.TargetFilter))
                    {
                        var filters = resolver.ParseFilters(config.TargetFilter);
                        candidates = resolver.ApplyFilters(candidates, filters, context);
                    }
                    context.Targets = candidates.Take(config.TargetCount).ToList();
                }
            }

            if (!handler.CanExecute(effect, context))
            {
                UnityEngine.Debug.LogWarning($"效果无法执行: {effect.Type}");
                return false;
            }

            handler.Execute(effect, context);
            return true;
        }

 
        /// <summary>
        /// 获取所有已注册的效果类型
        /// </summary>
        public static IEnumerable<AtomicEffectType> GetRegisteredTypes()
        {
            return _handlers.Keys;
        }

        /// <summary>
        /// 获取已注册处理器数量
        /// </summary>
        public static int HandlerCount => _handlers.Count;

        /// <summary>
        /// 检查效果类型是否已注册
        /// </summary>
        public static bool IsRegistered(AtomicEffectType type)
        {
            return _handlers.ContainsKey(type);
        }
    }
}
