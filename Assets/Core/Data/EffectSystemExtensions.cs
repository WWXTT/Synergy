using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 效果系统扩展
    /// 用于将 EffectData 转换为可运行的效果对象
    /// </summary>
    public static class EffectSystemExtensions
    {
        /// <summary>
        /// 效果逻辑工厂
        /// 根据效果缩写创建对应的 EffectLogic
        /// </summary>
        public class EffectLogicFactory
        {
            private static Dictionary<string, Type> _logicTypes = new Dictionary<string, Type>();

            static EffectLogicFactory()
            {
                // 注册默认效果逻辑
                RegisterLogic("DMG", typeof(DealDamageLogic));
                RegisterLogic("HEAL", typeof(HealLogic));
                RegisterLogic("DRAW", typeof(DrawCardLogic));
                RegisterLogic("DESTROY", typeof(DestroyTargetLogic));
                RegisterLogic("BUFF_ATK", typeof(BuffAttackLogic));
                RegisterLogic("BUFF_DEF", typeof(BuffLifeLogic));
                RegisterLogic("DEBUFF_ATK", typeof(DebuffAttackLogic));
                RegisterLogic("DEBUFF_DEF", typeof(DebuffLifeLogic));
            }

            /// <summary>
            /// 注册效果逻辑类型
            /// </summary>
            public static void RegisterLogic(string abbreviation, Type logicType)
            {
                if (string.IsNullOrEmpty(abbreviation))
                    throw new ArgumentException("Abbreviation cannot be null or empty", nameof(abbreviation));

                if (!typeof(EffectLogic).IsAssignableFrom(logicType))
                    throw new ArgumentException($"Type must be assignable to EffectLogic", nameof(logicType));

                _logicTypes[abbreviation.ToUpper()] = logicType;
            }

            /// <summary>
            /// 创建效果逻辑
            /// </summary>
            public static EffectLogic Create(string abbreviation)
            {
                string key = abbreviation?.ToUpper();
                if (!_logicTypes.TryGetValue(key, out var logicType))
                {
                    Debug.LogWarning($"No logic registered for effect abbreviation: {abbreviation}. Using fallback logic.");
                    return new FallbackLogic { Abbreviation = abbreviation };
                }

                return ScriptableObject.CreateInstance(logicType) as EffectLogic;
            }

            /// <summary>
            /// 创建效果逻辑（带参数）
            /// </summary>
            public static EffectLogic Create(string abbreviation, float parameters)
            {
                var logic = Create(abbreviation);

                // 尝试设置参数
                if (logic is IParameterizedEffect parameterized)
                {
                    parameterized.SetParameter(parameters);
                }

                return logic;
            }
        }

        /// <summary>
        /// 可参数化的效果接口
        /// </summary>
        public interface IParameterizedEffect
        {
            void SetParameter(float parameter);
        }

        /// <summary>
        /// 从 CardData 创建完整的卡牌 Effect 列表
        /// </summary>
        public static List<Effect> CreateEffectsFromData(CardData cardData)
        {
            var effects = new List<Effect>();

            foreach (var effectData in cardData.Effects)
            {
                Effect effect = CreateEffectFromData(effectData);
                if (effect != null)
                {
                    effects.Add(effect);
                }
            }

            return effects;
        }

        /// <summary>
        /// 从 EffectData 创建 Effect
        /// </summary>
        public static Effect CreateEffectFromData(EffectData effectData)
        {
            if (effectData == null) return null;

            // 根据效果速度决定效果类型
            Effect effect;
            switch (effectData.Speed)
            {
                case EffectSpeed.强制诱发:
                case EffectSpeed.可选诱发:
                    // 强制或可选诱发效果 -> 激活式效果
                    effect = ScriptableObject.CreateInstance<ActivatedAbility>();
                    effect.EffectName = effectData.Abbreviation;
                    effect.Description = effectData.Description;
                    effect.Speed = effectData.Speed;
                    effect.Logic = EffectLogicFactory.Create(effectData.Abbreviation, effectData.Parameters);
                    break;

                case EffectSpeed.自由时点:
                    // 自由时点自发 -> 静态效果或触发式
                    // 这里暂时用静态效果，可以根据需要调整
                    effect = ScriptableObject.CreateInstance<StaticAbility>();
                    effect.EffectName = effectData.Abbreviation;
                    effect.Description = effectData.Description;
                    effect.Speed = effectData.Speed;
                    effect.Logic = EffectLogicFactory.Create(effectData.Abbreviation, effectData.Parameters);
                    break;

                default:
                    effect = ScriptableObject.CreateInstance<ActivatedAbility>();
                    effect.EffectName = effectData.Abbreviation;
                    effect.Description = effectData.Description;
                    effect.Speed = effectData.Speed;
                    effect.Logic = EffectLogicFactory.Create(effectData.Abbreviation, effectData.Parameters);
                    break;
            }

            return effect;
        }

        /// <summary>
        /// 兜底效果逻辑 - 用于未注册的效果
        /// </summary>
        public class FallbackLogic : EffectLogic
        {
            public string Abbreviation { get; set; }
            public float Parameter { get; set; }

            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                Debug.LogWarning($"Executing fallback logic for effect: {Abbreviation} with parameter: {Parameter}");
                // 兜底效果什么都不做
            }
        }

        // ==================== 常用效果逻辑 ====================

        /// <summary>
        /// 造成伤害逻辑（扩展版）
        /// </summary>
        public class DealDamageLogicExtended : DealDamageLogic, IParameterizedEffect
        {
            public void SetParameter(float parameter)
            {
                Damage = (int)parameter;
            }
        }

        /// <summary>
        /// 回复生命逻辑
        /// </summary>
        public class HealLogic : EffectLogic, IParameterizedEffect
        {
            public int HealAmount { get; private set; }

            public void SetParameter(float parameter)
            {
                HealAmount = (int)parameter;
            }

            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                var target = effect.Targets[0] as Player;
                if (target == null) return;

                // 记录 Undo
                resolution.RegisterUndo(new LifeChangeUndo(target, -HealAmount));

                target.Life += HealAmount;
            }
        }

        /// <summary>
        /// 抽卡逻辑
        /// </summary>
        public class DrawCardLogic : EffectLogic, IParameterizedEffect
        {
            public int DrawCount { get; private set; }

            public void SetParameter(float parameter)
            {
                DrawCount = (int)parameter;
            }

            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                // TODO: 实现抽卡逻辑
                // 需要访问 GameCore 中的抽卡机制
                Debug.LogWarning($"DrawCardLogic: Draw {DrawCount} cards (TODO: implement)");
            }
        }

        /// <summary>
        /// 销毁目标逻辑
        /// </summary>
        public class DestroyTargetLogic : EffectLogic
        {
            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                // TODO: 实现销毁逻辑
                Debug.LogWarning("DestroyTargetLogic: Destroy target (TODO: implement)");
            }
        }

        /// <summary>
        /// 攻击力增益逻辑
        /// </summary>
        public class BuffAttackLogic : EffectLogic, IParameterizedEffect
        {
            public int BuffAmount { get; private set; }

            public void SetParameter(float parameter)
            {
                BuffAmount = (int)parameter;
            }

            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                // TODO: 实现攻击力增益逻辑
                Debug.LogWarning($"BuffAttackLogic: +{BuffAmount} attack (TODO: implement)");
            }
        }

        /// <summary>
        /// 生命值增益逻辑
        /// </summary>
        public class BuffLifeLogic : EffectLogic, IParameterizedEffect
        {
            public int BuffAmount { get; private set; }

            public void SetParameter(float parameter)
            {
                BuffAmount = (int)parameter;
            }

            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                // TODO: 实现生命值增益逻辑
                Debug.LogWarning($"BuffLifeLogic: +{BuffAmount} life (TODO: implement)");
            }
        }

        /// <summary>
        /// 攻击力削弱逻辑
        /// </summary>
        public class DebuffAttackLogic : EffectLogic, IParameterizedEffect
        {
            public int DebuffAmount { get; private set; }

            public void SetParameter(float parameter)
            {
                DebuffAmount = (int)parameter;
            }

            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                // TODO: 实现攻击力削弱逻辑
                Debug.LogWarning($"DebuffAttackLogic: -{DebuffAmount} attack (TODO: implement)");
            }
        }

        /// <summary>
        /// 生命值削弱逻辑
        /// </summary>
        public class DebuffLifeLogic : EffectLogic, IParameterizedEffect
        {
            public int DebuffAmount { get; private set; }

            public void SetParameter(float parameter)
            {
                DebuffAmount = (int)parameter;
            }

            public override void Execute(EffectInstance effect, EffectResolutionContext resolution)
            {
                // TODO: 实现生命值削弱逻辑
                Debug.LogWarning($"DebuffLifeLogic: -{DebuffAmount} life (TODO: implement)");
            }
        }
    }

    /// <summary>
    /// 效果扩展方法
    /// </summary>
    public static class EffectExtensions
    {
        /// <summary>
        /// 将 Effect 转换为 EffectData
        /// </summary>
        public static EffectData ToEffectData(this Effect effect)
        {
            return new EffectData
            {
                Abbreviation = effect.EffectName,
                Initiative = true, // 默认为主动效果
                Parameters = 0,
                Speed = effect.Speed,
                ManaType = ManaType.灰色, // 默认为灰色
                Description = effect.Description
            };
        }

        /// <summary>
        /// 将 Effect_table 转换为 EffectData
        /// </summary>
        public static EffectData ToEffectData(this Effect_table table)
        {
            return EffectData.FromEffectTable(table);
        }
    }
}
