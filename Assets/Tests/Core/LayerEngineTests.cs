using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// LayerEngine 单元测试
    /// 测试层引擎的持续效果添加、移除、特征计算和排序逻辑
    /// </summary>
    [TestFixture]
    public class LayerEngineTests
    {
        private LayerEngine _layerEngine;

        [SetUp]
        public void SetUp()
        {
            _layerEngine = new LayerEngine();
            EventManager.Instance.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.Instance.ClearAll();
        }

        #region 添加/移除效果测试

        [Test]
        public void AddLayerContinuousEffect_TargetEffect_AddedSuccessfully()
        {
            var entity = new TestEntityWithPower(0);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 5) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);

            int power = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(5, power);
        }

        [Test]
        public void AddLayerContinuousEffect_GlobalEffect_AddedSuccessfully()
        {
            var effect = CreateContinuousEffect(null, LayerType.Layer4,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 3) }
                });

            Assert.DoesNotThrow(() => _layerEngine.AddLayerContinuousEffect(effect));
        }

        [Test]
        public void RemoveLayerContinuousEffect_RemovesEffect()
        {
            var entity = new TestEntityWithPower(5);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>());

            _layerEngine.AddLayerContinuousEffect(effect);
            _layerEngine.RemoveLayerContinuousEffect(effect);

            Assert.AreEqual(5, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void RemoveLayerContinuousEffect_GlobalEffect_RemovesSuccessfully()
        {
            var effect = CreateContinuousEffect(null, LayerType.Layer4,
                new Dictionary<CharacteristicType, ICharacteristicModification>());

            _layerEngine.AddLayerContinuousEffect(effect);
            Assert.DoesNotThrow(() => _layerEngine.RemoveLayerContinuousEffect(effect));
        }

        #endregion

        #region CDA 测试

        [Test]
        public void AddCDA_SetsCharacteristicValue()
        {
            var entity = new TestEntityWithPower(3);
            var cda = new CharacteristicDefiningAbility
            {
                Source = entity,
                DefinedEntity = entity,
                DefinitionType = CharacteristicDefiningType.DefinesStats,
                DefinedValue = 10,
                SequenceNumber = 1
            };

            _layerEngine.AddCDA(cda);
            int power = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(10, power); // CDA overrides base value
        }

        [Test]
        public void AddCDA_GlobalCDA_AppliesCorrectly()
        {
            var entity = new TestEntityWithPower(5);
            var cda = new CharacteristicDefiningAbility
            {
                Source = entity,
                DefinedEntity = null, // global
                DefinitionType = CharacteristicDefiningType.DefinesStats,
                DefinedValue = 99,
                SequenceNumber = 1
            };

            _layerEngine.AddCDA(cda);
            int power = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(99, power);
        }

        [Test]
        public void RemoveCDA_RevertsToBaseValue()
        {
            var entity = new TestEntityWithPower(3);
            var cda = new CharacteristicDefiningAbility
            {
                Source = entity,
                DefinedEntity = entity,
                DefinitionType = CharacteristicDefiningType.DefinesStats,
                DefinedValue = 10,
                SequenceNumber = 1
            };

            _layerEngine.AddCDA(cda);
            _layerEngine.RemoveCDA(cda);

            int power = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(3, power); // reverts to base
        }

        [Test]
        public void MultipleCDAs_OldestWins()
        {
            var entity = new TestEntityWithPower(1);

            var cda1 = new CharacteristicDefiningAbility
            {
                Source = entity,
                DefinedEntity = entity,
                DefinitionType = CharacteristicDefiningType.DefinesStats,
                DefinedValue = 5,
                SequenceNumber = 10
            };

            var cda2 = new CharacteristicDefiningAbility
            {
                Source = entity,
                DefinedEntity = entity,
                DefinitionType = CharacteristicDefiningType.DefinesStats,
                DefinedValue = 8,
                SequenceNumber = 5 // older
            };

            _layerEngine.AddCDA(cda1);
            _layerEngine.AddCDA(cda2);

            int power = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(8, power); // oldest (sequence 5) wins
        }

        #endregion

        #region 排序测试

        [Test]
        public void Effects_SortedByLayer_ThenBySequence()
        {
            var entity = new TestEntityWithPower(0);

            // 添加不同层的效果
            var effect1 = CreateContinuousEffect(entity, LayerType.Layer4,
                new Dictionary<CharacteristicType, ICharacteristicModification>(), seq: 100);
            var effect2 = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>(), seq: 200);

            _layerEngine.AddLayerContinuousEffect(effect1);
            _layerEngine.AddLayerContinuousEffect(effect2);

            // 不应抛出异常
            Assert.DoesNotThrow(() => _layerEngine.CalculatePower(entity));
        }

        #endregion

        #region ClearAll 测试

        [Test]
        public void ClearAll_RemovesAllEffects()
        {
            var entity = new TestEntityWithPower(5);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>());
            var cda = new CharacteristicDefiningAbility
            {
                Source = entity,
                DefinedEntity = entity,
                DefinitionType = CharacteristicDefiningType.DefinesStats,
                DefinedValue = 10,
                SequenceNumber = 1
            };

            _layerEngine.AddLayerContinuousEffect(effect);
            _layerEngine.AddCDA(cda);
            _layerEngine.ClearAll();

            int power = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(5, power); // back to base
        }

        #endregion

        #region 类型安全修饰器测试

        [Test]
        public void IntAddModification_IncreasesPower()
        {
            var entity = new TestEntityWithPower(5);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 3) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);

            Assert.AreEqual(8, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void IntAddModification_NegativeDelta_DecreasesPower()
        {
            var entity = new TestEntityWithPower(10);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, -4) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);

            Assert.AreEqual(6, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void IntSetModification_OverridesBaseValue()
        {
            var entity = new TestEntityWithPower(5);
            var effect = CreateContinuousEffect(entity, LayerType.Layer4,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntSetModification(CharacteristicType.Power, 20) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);

            Assert.AreEqual(20, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void MultipleModifications_StackInLayerOrder()
        {
            var entity = new TestEntityWithPower(5);

            // Layer3 先执行：+3
            var effect1 = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 3) }
                }, seq: 1);

            // Layer4 后执行：+2
            var effect2 = CreateContinuousEffect(entity, LayerType.Layer4,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 2) }
                }, seq: 2);

            _layerEngine.AddLayerContinuousEffect(effect1);
            _layerEngine.AddLayerContinuousEffect(effect2);

            // 5 (base) + 3 + 2 = 10
            Assert.AreEqual(10, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void ToughnessModification_AppliedCorrectly()
        {
            var entity = new TestEntityWithPowerAndLife(5, 10);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Toughness, new IntAddModification(CharacteristicType.Toughness, 3) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);

            Assert.AreEqual(13, _layerEngine.CalculateToughness(entity));
        }

        #endregion

        #region HasEffectType 测试

        [Test]
        public void HasEffectType_WhenEffectExists_ReturnsTrue()
        {
            var entity = new TestEntity();
            var layerEffect = new LayerContinuousEffect
            {
                SourceEffect = null,
                TargetEntity = entity,
                Layer = LayerType.Layer3,
                Modifications = new Dictionary<CharacteristicType, ICharacteristicModification>()
            };

            _layerEngine.AddLayerContinuousEffect(layerEffect);
            Assert.IsFalse(_layerEngine.HasEffectType(null));
        }

        [Test]
        public void HasEffectType_WhenNoEffects_ReturnsFalse()
        {
            Assert.IsFalse(_layerEngine.HasEffectType(null));
        }

        #endregion

        #region 缓存（脏标记）测试

        [Test]
        public void Cache_SecondCallReturnsCachedValue()
        {
            var entity = new TestEntityWithPower(5);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 3) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);

            // 第一次调用：计算并缓存
            int first = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(8, first);

            // 第二次调用：应从缓存返回（结果一致）
            int second = _layerEngine.CalculatePower(entity);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void Cache_InvalidatedWhenEffectRemoved()
        {
            var entity = new TestEntityWithPower(5);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 3) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);
            Assert.AreEqual(8, _layerEngine.CalculatePower(entity));

            // 移除效果应使缓存失效
            _layerEngine.RemoveLayerContinuousEffect(effect);
            Assert.AreEqual(5, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void Cache_InvalidatedWhenCDARemoved()
        {
            var entity = new TestEntityWithPower(5);
            var cda = new CharacteristicDefiningAbility
            {
                Source = entity,
                DefinedEntity = entity,
                DefinitionType = CharacteristicDefiningType.DefinesStats,
                DefinedValue = 99,
                SequenceNumber = 1
            };

            _layerEngine.AddCDA(cda);
            Assert.AreEqual(99, _layerEngine.CalculatePower(entity));

            _layerEngine.RemoveCDA(cda);
            Assert.AreEqual(5, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void Cache_InvalidatedWhenNewEffectAdded()
        {
            var entity = new TestEntityWithPower(5);
            var effect1 = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 3) }
                });

            _layerEngine.AddLayerContinuousEffect(effect1);
            Assert.AreEqual(8, _layerEngine.CalculatePower(entity));

            // 添加新效果应使缓存失效
            var effect2 = CreateContinuousEffect(entity, LayerType.Layer4,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 2) }
                });

            _layerEngine.AddLayerContinuousEffect(effect2);
            Assert.AreEqual(10, _layerEngine.CalculatePower(entity));
        }

        [Test]
        public void InvalidateAllCache_ClearsAllCachedValues()
        {
            var entity = new TestEntityWithPower(5);
            var effect = CreateContinuousEffect(entity, LayerType.Layer3,
                new Dictionary<CharacteristicType, ICharacteristicModification>
                {
                    { CharacteristicType.Power, new IntAddModification(CharacteristicType.Power, 3) }
                });

            _layerEngine.AddLayerContinuousEffect(effect);
            Assert.AreEqual(8, _layerEngine.CalculatePower(entity));

            _layerEngine.InvalidateAllCache();

            // 缓存被清除，重新计算
            Assert.AreEqual(8, _layerEngine.CalculatePower(entity));
        }

        #endregion

        #region 辅助方法

        private static LayerContinuousEffect CreateContinuousEffect(
            Entity target,
            LayerType layer,
            Dictionary<CharacteristicType, ICharacteristicModification> modifications,
            int seq = 1)
        {
            return new LayerContinuousEffect
            {
                SourceEffect = null,
                SourceEntity = null,
                TargetEntity = target,
                Layer = layer,
                StartTime = DateTime.Now,
                SequenceNumber = seq,
                Duration = DurationType.Permanent,
                IsActive = true,
                Modifications = modifications ?? new Dictionary<CharacteristicType, ICharacteristicModification>()
            };
        }

        #endregion
    }

    /// <summary>
    /// 测试用简单实体
    /// </summary>
    public class TestEntity : Entity
    {
        public TestEntity() : base(createTimestamp: false) { }
    }

    /// <summary>
    /// 测试用带攻击力的实体
    /// </summary>
    public class TestEntityWithPower : Entity, IHasPower
    {
        public int Power { get; set; }

        public TestEntityWithPower(int power) : base(createTimestamp: false)
        {
            Power = power;
        }
    }

    /// <summary>
    /// 测试用带攻击力和生命值的实体
    /// </summary>
    public class TestEntityWithPowerAndLife : Entity, IHasPower, IHasLife
    {
        public int Power { get; set; }
        public int Life { get; set; }

        public TestEntityWithPowerAndLife(int power, int life) : base(createTimestamp: false)
        {
            Power = power;
            Life = life;
        }
    }

    /// <summary>
    /// 测试用带关键词的可横置卡牌
    /// </summary>
    public class TestCreatureCard : Card, IHasPower, IHasLife, ITappable, IHasKeywords
    {
        public int Power { get; set; }
        public int Life { get; set; }
        public bool IsTapped { get; set; }
        public HashSet<string> Keywords { get; set; } = new HashSet<string>();

        public TestCreatureCard(string id, int power, int life) : base()
        {
            ID = id;
            Power = power;
            Life = life;
        }

        public void AddKeyword(string keywordId)
        {
            Keywords.Add(keywordId);
        }

        public void RemoveKeyword(string keywordId)
        {
            Keywords.Remove(keywordId);
        }

        public bool HasKeyword(string keywordId)
        {
            return Keywords.Contains(keywordId);
        }
    }
}
