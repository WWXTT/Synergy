using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 修改器接口
    /// 用于在层系统中修改实体属性
    /// </summary>
    public interface IModifier
    {
        /// <summary>
        /// 层级别（Layer 1-5）
        /// </summary>
        int Layer { get; }

        /// <summary>
        /// 修改器是否有效
        /// </summary>
        bool IsValid();

        /// <summary>
        /// 应用修改器到值
        /// </summary>
        int Apply(int value);
    }

    /// <summary>
    /// 修改器系统
    /// 管理所有修改器，按层提供修改器查询
    /// </summary>
    public class ModifierSystem
    {
        private List<IModifier> _modifiers = new List<IModifier>();

        /// <summary>
        /// 添加修改器
        /// </summary>
        public void Add(IModifier modifier)
        {
            _modifiers.Add(modifier);
        }

        /// <summary>
        /// 移除修改器
        /// </summary>
        public void Remove(IModifier modifier)
        {
            _modifiers.Remove(modifier);
        }

        /// <summary>
        /// 清空所有修改器
        /// </summary>
        public void Clear()
        {
            _modifiers.Clear();
        }

        /// <summary>
        /// 获取指定实体和层级的所有修改器
        /// </summary>
        public IEnumerable<IModifier> GetModifiersFor(Entity entity, int layer)
        {
            foreach (var m in _modifiers)
            {
                if (m.Layer == layer && m.IsValid())
                {
                    // TODO: 检查修改器是否应用于该实体
                    yield return m;
                }
            }
        }

        /// <summary>
        /// 获取修改器数量
        /// </summary>
        public int Count => _modifiers.Count;
    }

    /// <summary>
    /// 属性计算器
    /// 根据修改器系统计算实体属性（如攻击力、生命值等）
    /// </summary>
    public class AttributeCalculator
    {
        private ModifierSystem _modifierSystem;

        public AttributeCalculator(ModifierSystem system)
        {
            _modifierSystem = system;
        }

        /// <summary>
        /// 计算攻击力
        /// 按 Layer 1-5 顺序应用修改器
        /// </summary>
        public int CalculateAttack(Unit unit)
        {
            int value = unit.BaseAttack;

            for (int layer = 1; layer <= 5; layer++)
            {
                foreach (var m in _modifierSystem.GetModifiersFor(unit, layer))
                {
                    value = m.Apply(value);
                }
            }

            return value;
        }

        /// <summary>
        /// 计算生命值
        /// 按 Layer 1-5 顺序应用修改器
        /// </summary>
        public int CalculateLife(Player player)
        {
            int value = player.Life;

            for (int layer = 1; layer <= 5; layer++)
            {
                foreach (var m in _modifierSystem.GetModifiersFor(player, layer))
                {
                    value = m.Apply(value);
                }
            }

            return value;
        }

        /// <summary>
        /// 通用属性计算
        /// </summary>
        public int CalculateAttribute(Entity entity, int baseValue, int startLayer = 1, int endLayer = 5)
        {
            int value = baseValue;

            for (int layer = startLayer; layer <= endLayer; layer++)
            {
                foreach (var m in _modifierSystem.GetModifiersFor(entity, layer))
                {
                    value = m.Apply(value);
                }
            }

            return value;
        }
    }
}
