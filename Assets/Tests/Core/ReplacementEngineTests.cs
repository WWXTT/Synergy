using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// 替代引擎单元测试
    /// 测试替代效果的注册、查询、检查和替代应用
    /// </summary>
    [TestFixture]
    public class ReplacementEngineTests
    {
        private ReplacementEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _engine = new ReplacementEngine();
            EventManager.Instance.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.Instance.ClearAll();
        }

        #region 注册与注销测试

        [Test]
        public void RegisterReplacementEffect_SingleEffect_RegisteredSuccessfully()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));

            Assert.IsTrue(_engine.HasReplacementEffect(typeof(CardDestroyEvent)));
        }

        [Test]
        public void RegisterReplacementEffect_MultipleEffectsOfType_AllRegistered()
        {
            var effect1 = new DestructionReplacement();
            var effect2 = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect1, typeof(CardDestroyEvent));
            _engine.RegisterReplacementEffect(effect2, typeof(CardDestroyEvent));

            var effects = _engine.GetReplacementEffects(typeof(CardDestroyEvent));
            Assert.AreEqual(2, effects.Count);
        }

        [Test]
        public void RegisterReplacementEffect_DifferentTypes_TrackedSeparately()
        {
            var destroyEffect = new DestructionReplacement();
            var damageEffect = new DamageReplacement();
            _engine.RegisterReplacementEffect(destroyEffect, typeof(CardDestroyEvent));
            _engine.RegisterReplacementEffect(damageEffect, typeof(DamageEvent));

            Assert.IsTrue(_engine.HasReplacementEffect(typeof(CardDestroyEvent)));
            Assert.IsTrue(_engine.HasReplacementEffect(typeof(DamageEvent)));
        }

        [Test]
        public void UnregisterReplacementEffect_RemovesEffect()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));
            _engine.UnregisterReplacementEffect(effect, typeof(CardDestroyEvent));

            Assert.IsFalse(_engine.HasReplacementEffect(typeof(CardDestroyEvent)));
        }

        [Test]
        public void UnregisterReplacementEffect_OnlyRemovesTarget_NotOthers()
        {
            var effect1 = new DestructionReplacement();
            var effect2 = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect1, typeof(CardDestroyEvent));
            _engine.RegisterReplacementEffect(effect2, typeof(CardDestroyEvent));

            _engine.UnregisterReplacementEffect(effect1, typeof(CardDestroyEvent));

            var remaining = _engine.GetReplacementEffects(typeof(CardDestroyEvent));
            Assert.AreEqual(1, remaining.Count);
        }

        #endregion

        #region 查询测试

        [Test]
        public void HasReplacementEffect_NoRegistration_ReturnsFalse()
        {
            Assert.IsFalse(_engine.HasReplacementEffect(typeof(CardDestroyEvent)));
        }

        [Test]
        public void GetReplacementEffects_NoRegistration_ReturnsEmptyList()
        {
            var effects = _engine.GetReplacementEffects(typeof(CardDestroyEvent));
            Assert.IsNotNull(effects);
            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void GetReplacementEffects_WithRegistration_ReturnsCorrectEffects()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));

            var effects = _engine.GetReplacementEffects(typeof(CardDestroyEvent));
            Assert.AreEqual(1, effects.Count);
            Assert.AreSame(effect, effects[0]);
        }

        #endregion

        #region CheckReplacements - 无替代场景

        [Test]
        public void CheckReplacements_NoReplacementEffects_ReturnsUnreplacedContext()
        {
            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = new Card(),
                Reason = DestroyReason.Destroyed,
                Source = null
            };

            var context = _engine.CheckReplacements(destroyEvent);

            Assert.IsNotNull(context);
            Assert.IsFalse(context.IsReplaced);
            Assert.AreSame(destroyEvent, context.OriginalEvent);
            Assert.IsNull(context.ReplacementEvent);
        }

        [Test]
        public void CheckReplacements_WrongEventTypeRegistration_NoReplacement()
        {
            // 注册了一个 DamageEvent 的替代效果
            var damageEffect = new DamageReplacement();
            _engine.RegisterReplacementEffect(damageEffect, typeof(DamageEvent));

            // 发送的是 CardDestroyEvent
            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = new Card(),
                Reason = DestroyReason.Destroyed
            };

            var context = _engine.CheckReplacements(destroyEvent);
            Assert.IsFalse(context.IsReplaced);
        }

        #endregion

        #region CheckReplacements - 销毁替代

        [Test]
        public void CheckReplacements_DestroyedReason_IsReplaced()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));

            var card = new Card();
            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = card,
                Reason = DestroyReason.Destroyed
            };

            var context = _engine.CheckReplacements(destroyEvent);

            Assert.IsTrue(context.IsReplaced);
            Assert.IsNotNull(context.ReplacementEvent);
            Assert.IsInstanceOf<CardBanishEvent>(context.ReplacementEvent);
        }

        [Test]
        public void CheckReplacements_DestroyedReason_BanishCardMatchesOriginal()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));

            var card = new Card();
            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = card,
                Reason = DestroyReason.Destroyed,
                Source = null
            };

            var context = _engine.CheckReplacements(destroyEvent);
            var banishEvent = context.ReplacementEvent as CardBanishEvent;

            Assert.IsNotNull(banishEvent);
            Assert.AreSame(card, banishEvent.BanishedCard);
        }

        [Test]
        public void CheckReplacements_CombatReason_DestructionReplacementDoesNotApply()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));

            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = new Card(),
                Reason = DestroyReason.Combat
            };

            var context = _engine.CheckReplacements(destroyEvent);
            Assert.IsFalse(context.IsReplaced);
        }

        #endregion

        #region CheckReplacements - 伤害替代

        [Test]
        public void CheckReplacements_DamageEvent_WithDamageReplacement_IsReplaced()
        {
            var effect = new DamageReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(DamageEvent));

            var damageEvent = new DamageEvent
            {
                Source = null,
                Target = null,
                Amount = 5
            };

            var context = _engine.CheckReplacements(damageEvent);
            Assert.IsTrue(context.IsReplaced);
            Assert.IsNotNull(context.ReplacementEvent);
            Assert.IsInstanceOf<DamageEvent>(context.ReplacementEvent);
        }

        #endregion

        #region ReplacementContext 测试

        [Test]
        public void ReplacementContext_GetFinalEvent_NoReplacement_ReturnsOriginal()
        {
            var destroyEvent = new CardDestroyEvent { DestroyedCard = new Card() };
            var context = new ReplacementContext { OriginalEvent = destroyEvent };

            Assert.AreSame(destroyEvent, context.GetFinalEvent());
        }

        [Test]
        public void ReplacementContext_GetFinalEvent_WithReplacement_ReturnsReplacement()
        {
            var originalEvent = new CardDestroyEvent { DestroyedCard = new Card() };
            var replacementEvent = new CardBanishEvent();
            var context = new ReplacementContext
            {
                OriginalEvent = originalEvent,
                ReplacementEvent = replacementEvent
            };

            Assert.AreSame(replacementEvent, context.GetFinalEvent());
        }

        [Test]
        public void ReplacementContext_AppliedReplacements_TracksApplications()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));

            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = new Card(),
                Reason = DestroyReason.Destroyed
            };

            var context = _engine.CheckReplacements(destroyEvent);

            Assert.AreEqual(1, context.AppliedReplacements.Count);
            Assert.IsNotNull(context.AppliedReplacements[0].SourceEffect);
            Assert.AreSame(destroyEvent, context.AppliedReplacements[0].ReplacedEvent);
            Assert.IsNotNull(context.AppliedReplacements[0].ReplacementEvent);
        }

        #endregion

        #region ClearAll 测试

        [Test]
        public void ClearAll_RemovesAllEffects()
        {
            var destroyEffect = new DestructionReplacement();
            var damageEffect = new DamageReplacement();
            _engine.RegisterReplacementEffect(destroyEffect, typeof(CardDestroyEvent));
            _engine.RegisterReplacementEffect(damageEffect, typeof(DamageEvent));

            _engine.ClearAll();

            Assert.IsFalse(_engine.HasReplacementEffect(typeof(CardDestroyEvent)));
            Assert.IsFalse(_engine.HasReplacementEffect(typeof(DamageEvent)));
        }

        [Test]
        public void ClearAll_CheckReplacementsReturnsFalse()
        {
            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));
            _engine.ClearAll();

            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = new Card(),
                Reason = DestroyReason.Destroyed
            };

            var context = _engine.CheckReplacements(destroyEvent);
            Assert.IsFalse(context.IsReplaced);
        }

        #endregion

        #region 替代事件发布测试

        [Test]
        public void CheckReplacements_WhenReplaced_PublishesReplacementEvent()
        {
            ReplacementEvent receivedEvent = null;
            EventManager.Instance.Subscribe<ReplacementEvent>(e => receivedEvent = e);

            var effect = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect, typeof(CardDestroyEvent));

            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = new Card(),
                Reason = DestroyReason.Destroyed
            };

            _engine.CheckReplacements(destroyEvent);

            Assert.IsNotNull(receivedEvent);
            Assert.AreSame(destroyEvent, receivedEvent.OriginalEvent);
            Assert.AreEqual(1, receivedEvent.AppliedReplacements.Count);
        }

        #endregion

        #region 多重替代链测试

        [Test]
        public void CheckReplacements_MultipleEffects_AppliesInSequence()
        {
            var effect1 = new DestructionReplacement();
            var effect2 = new DestructionReplacement();
            _engine.RegisterReplacementEffect(effect1, typeof(CardDestroyEvent));
            _engine.RegisterReplacementEffect(effect2, typeof(CardDestroyEvent));

            var destroyEvent = new CardDestroyEvent
            {
                DestroyedCard = new Card(),
                Reason = DestroyReason.Destroyed
            };

            var context = _engine.CheckReplacements(destroyEvent);

            // 第一个替代后变成 CardBanishEvent，
            // 第二个 DestructionReplacement 只能替代 CardDestroyEvent，
            // 所以 BanishEvent 不满足 CanReplace 条件，链中断
            Assert.IsTrue(context.IsReplaced);
            Assert.AreEqual(1, context.AppliedReplacements.Count);
        }

        #endregion
    }
}
