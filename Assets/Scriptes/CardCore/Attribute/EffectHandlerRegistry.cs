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

            // 目标解析：调用方已预选目标（如玩家施放法术时指定）则直接沿用，
            // 否则按原子效果配置自动解析。触发式效果不预选 → 走自动解析。
            bool hasPreselectedTargets = context.Targets != null && context.Targets.Count > 0;
            if (!hasPreselectedTargets)
            {
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
                        // TargetCount: 0=全部, <0=任意（全部）, >0=取前 N 个
                        context.Targets = config.TargetCount > 0
                            ? candidates.Take(config.TargetCount).ToList()
                            : candidates;
                    }
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
