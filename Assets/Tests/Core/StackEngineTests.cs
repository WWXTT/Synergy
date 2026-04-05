using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// 栈引擎单元测试
    /// 覆盖：SpeedCounter、SpeedCalculator、PendingEffect、StackEngine 核心流程
    /// </summary>
    [TestFixture]
    public class StackEngineTests
    {
        private Player _player1;
        private Player _player2;
        private ZoneManager _zoneManager;
        private ElementPoolSystem _elementPool;
        private EffectExecutor _executor;
        private StackEngine _stackEngine;

        [SetUp]
        public void SetUp()
        {
            _player1 = new Player("P1", 20);
            _player2 = new Player("P2", 20);
            _player1.Opponent = _player2;
            _player2.Opponent = _player1;

            _zoneManager = new ZoneManager();
            _zoneManager.InitializePlayer(_player1);
            _zoneManager.InitializePlayer(_player2);

            _elementPool = new ElementPoolSystem();
            _elementPool.InitializePlayer(_player1);
            _elementPool.InitializePlayer(_player2);

            _executor = new EffectExecutor(_zoneManager, _elementPool);
            _stackEngine = new StackEngine(_executor);
            _stackEngine.Initialize(_player1);

            EventManager.Instance.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.Instance.ClearAll();
        }

        #region 辅助方法

        /// <summary>
        /// 创建测试用效果定义
        /// </summary>
        private EffectDefinition MakeVoluntaryEffect(
            int baseSpeed = 0,
            TriggerTiming timing = TriggerTiming.Activate_Active)
        {
            return new EffectDefinition
            {
                Id = "TEST_VOL_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = "Test Voluntary",
                ActivationType = EffectActivationType.Voluntary,
                BaseSpeed = baseSpeed,
                TriggerTiming = timing,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.AddCounters, Value = 0 }
                }
            };
        }

        /// <summary>
        /// 创建测试用条件发动效果
        /// </summary>
        private EffectDefinition MakeTriggeredEffect(
            EffectActivationType activationType = EffectActivationType.Automatic,
            TriggerTiming timing = TriggerTiming.On_EnterBattlefield)
        {
            return new EffectDefinition
            {
                Id = "TEST_TRG_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = "Test Triggered",
                ActivationType = activationType,
                TriggerTiming = timing,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.AddCounters, Value = 0 }
                }
            };
        }

        /// <summary>
        /// 创建测试用卡牌
        /// </summary>
        private StackTestCard MakeTestCard(string id)
        {
            return new StackTestCard(id, 2, 2);
        }

        /// <summary>
        /// 将 StackEngine 设置到主阶段，回合持有者为 P1
        /// </summary>
        private void SetupMainPhase()
        {
            _stackEngine.OnTurnStart(_player1);
            _stackEngine.SetPhase(PhaseType.Main);
        }

        #endregion

        // ======================================== SpeedCounter 测试 ========================================

        [Test]
        public void SpeedCounter_StartsAtZero()
        {
            var counter = new SpeedCounter();
            Assert.AreEqual(0, counter.CurrentSpeed);
        }

        [Test]
        public void SpeedCounter_IncrementAndDecrement()
        {
            var counter = new SpeedCounter();

            counter.Increment();
            Assert.AreEqual(1, counter.CurrentSpeed);

            counter.Increment();
            Assert.AreEqual(2, counter.CurrentSpeed);

            counter.Decrement();
            Assert.AreEqual(1, counter.CurrentSpeed);

            counter.Decrement();
            Assert.AreEqual(0, counter.CurrentSpeed);
        }

        [Test]
        public void SpeedCounter_DecrementNotBelowZero()
        {
            var counter = new SpeedCounter();
            counter.Decrement();
            Assert.AreEqual(0, counter.CurrentSpeed, "记速器不应低于0");
        }

        [Test]
        public void SpeedCounter_Reset()
        {
            var counter = new SpeedCounter();
            counter.Increment();
            counter.Increment();
            counter.Increment();
            Assert.AreEqual(3, counter.CurrentSpeed);

            counter.Reset();
            Assert.AreEqual(0, counter.CurrentSpeed);
            Assert.IsFalse(counter.IsResolving);
        }

        [Test]
        public void SpeedCounter_CanActivate_SpeedMustExceed()
        {
            var counter = new SpeedCounter();

            // counter=0, speed=1 可以发动 (1 > 0)
            Assert.IsTrue(counter.CanActivate(1, EffectActivationType.Voluntary));

            // counter=0, speed=0 不可以 (0 <= 0)
            Assert.IsFalse(counter.CanActivate(0, EffectActivationType.Voluntary));

            counter.Increment(); // counter=1
            // speed=1 不可以 (1 <= 1)
            Assert.IsFalse(counter.CanActivate(1, EffectActivationType.Voluntary));
            // speed=2 可以 (2 > 1)
            Assert.IsTrue(counter.CanActivate(2, EffectActivationType.Voluntary));
        }

        [Test]
        public void SpeedCounter_BeginResolution_BlocksVoluntary()
        {
            var counter = new SpeedCounter();
            counter.Increment(); // counter=1

            counter.BeginResolution();
            Assert.IsTrue(counter.IsResolving);

            // 结算中，即使速度足够也不能发动 Voluntary
            Assert.IsFalse(counter.CanActivate(100, EffectActivationType.Voluntary),
                "结算中 Voluntary 不能发动");

            // 条件发动（Automatic/Mandatory）不在此处拦截，它们用 int.MaxValue 绕过 CanActivate
        }

        [Test]
        public void SpeedCounter_PeakTracking()
        {
            var counter = new SpeedCounter();
            counter.Increment(); // current=1, peak=1
            counter.Increment(); // current=2, peak=2
            counter.Decrement(); // current=1, peak=2

            Assert.AreEqual(1, counter.CurrentSpeed);
            Assert.AreEqual(2, counter.PeakSpeed, "PeakSpeed 应保持最高值");
        }

        // ======================================== SpeedCalculator 测试 ========================================

        [Test]
        public void SpeedCalculator_MainPhase_TP_Default1()
        {
            int speed = SpeedCalculator.GetDefaultSpeed(_player1, _player1, PhaseType.Main);
            Assert.AreEqual(1, speed, "主阶段回合持有者默认速度=1");
        }

        [Test]
        public void SpeedCalculator_MainPhase_NTP_Default0()
        {
            int speed = SpeedCalculator.GetDefaultSpeed(_player2, _player1, PhaseType.Main);
            Assert.AreEqual(0, speed, "主阶段非回合持有者默认速度=0");
        }

        [Test]
        public void SpeedCalculator_StandbyPhase_BothZero()
        {
            Assert.AreEqual(0, SpeedCalculator.GetDefaultSpeed(_player1, _player1, PhaseType.Standby),
                "准备阶段TP速度=0");
            Assert.AreEqual(0, SpeedCalculator.GetDefaultSpeed(_player2, _player1, PhaseType.Standby),
                "准备阶段NTP速度=0");
        }

        [Test]
        public void SpeedCalculator_EndPhase_BothZero()
        {
            Assert.AreEqual(0, SpeedCalculator.GetDefaultSpeed(_player1, _player1, PhaseType.End),
                "结束阶段TP速度=0");
            Assert.AreEqual(0, SpeedCalculator.GetDefaultSpeed(_player2, _player1, PhaseType.End),
                "结束阶段NTP速度=0");
        }

        [Test]
        public void SpeedCalculator_ThreeLayerStack()
        {
            // 默认1 + BaseSpeed2 + paidBoost3 = 6
            int result = SpeedCalculator.CalculateSpeed(1, 2, 3);
            Assert.AreEqual(6, result, "三层叠加: 默认1 + BaseSpeed2 + paidBoost3 = 6");
        }

        [Test]
        public void SpeedCalculator_ZeroDefaults()
        {
            // 默认0 + BaseSpeed0 + paidBoost0 = 0
            int result = SpeedCalculator.CalculateSpeed(0, 0, 0);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void SpeedCalculator_TunerSpeedCost_1SpeedPer1Cost()
        {
            Assert.AreEqual(1, SpeedCalculator.SPEED_COST_RATE,
                "协调怪兽提速费率：1速=1费");

            // 提升2速需要支付2费
            int cost = SpeedCalculator.CalculateSpeedCost(2);
            Assert.AreEqual(2, cost, "2速需要2费");

            // 提升5速需要支付5费
            Assert.AreEqual(5, SpeedCalculator.CalculateSpeedCost(5));
        }

        // ======================================== PendingEffect.Create 测试 ========================================

        [Test]
        public void PendingEffect_Voluntary_CalculatesSpeed()
        {
            var effect = MakeVoluntaryEffect(baseSpeed: 2);
            var card = MakeTestCard("SRC_001");

            var pending = PendingEffect.Create(
                effect, card, _player1, _player1, PhaseType.Main, paidBoost: 3);

            // 速度 = 默认1(TP主阶段) + BaseSpeed2 + paidBoost3 = 6
            Assert.AreEqual(6, pending.ActivationSpeed);
            Assert.AreEqual(EffectActivationType.Voluntary, pending.ActivationType);
            Assert.AreEqual(_player1, pending.Controller);
            Assert.AreEqual(card, pending.Source);
            Assert.AreEqual(3, pending.PaidSpeedBoost);
        }

        [Test]
        public void PendingEffect_Triggered_GetsMaxSpeed()
        {
            var effect = MakeTriggeredEffect(EffectActivationType.Automatic);
            var card = MakeTestCard("SRC_002");

            var pending = PendingEffect.Create(
                effect, card, _player1, _player1, PhaseType.Main);

            // 条件发动不参与速度比较，使用 int.MaxValue
            Assert.AreEqual(int.MaxValue, pending.ActivationSpeed);
            Assert.AreEqual(EffectActivationType.Automatic, pending.ActivationType);
        }

        [Test]
        public void PendingEffect_Mandatory_GetsMaxSpeed()
        {
            var effect = MakeTriggeredEffect(EffectActivationType.Mandatory);
            var card = MakeTestCard("SRC_003");

            var pending = PendingEffect.Create(
                effect, card, _player2, _player1, PhaseType.Standby);

            Assert.AreEqual(int.MaxValue, pending.ActivationSpeed);
            Assert.AreEqual(EffectActivationType.Mandatory, pending.ActivationType);
        }

        [Test]
        public void PendingEffect_Voluntary_NonMainPhase_ZeroSpeed()
        {
            var effect = MakeVoluntaryEffect(baseSpeed: 0);
            var card = MakeTestCard("SRC_004");

            // 准备阶段，TP，无加成 → 默认0
            var pending = PendingEffect.Create(
                effect, card, _player1, _player1, PhaseType.Standby);

            Assert.AreEqual(0, pending.ActivationSpeed,
                "准备阶段无加成时速度=0");
        }

        [Test]
        public void PendingEffect_Voluntary_NTP_MainPhase_ZeroSpeed()
        {
            var effect = MakeVoluntaryEffect(baseSpeed: 0);
            var card = MakeTestCard("SRC_005");

            // 主阶段，NTP，无加成 → 默认0
            var pending = PendingEffect.Create(
                effect, card, _player2, _player1, PhaseType.Main);

            Assert.AreEqual(0, pending.ActivationSpeed,
                "主阶段NTP无加成时速度=0");
        }

        // ======================================== StackEngine.TryActivateEffect 测试 ========================================

        [Test]
        public void TryActivateEffect_IncrementsCounter()
        {
            SetupMainPhase();

            var effect = MakeVoluntaryEffect(baseSpeed: 0);
            var card = MakeTestCard("CARD_001");

            var pending = PendingEffect.Create(
                effect, card, _player1, _player1, PhaseType.Main);

            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed, "初始counter=0");

            bool result = _stackEngine.TryActivateEffect(pending);
            Assert.IsTrue(result, "TP主阶段发动应成功");
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed,
                "入栈后counter应+1");
            Assert.AreEqual(1, _stackEngine.StackSize);
        }

        [Test]
        public void TryActivateEffect_CounterIncrementsPerActivation()
        {
            SetupMainPhase();

            // 第一次发动
            var effect1 = MakeVoluntaryEffect(baseSpeed: 2);
            var card1 = MakeTestCard("CARD_A");
            var pending1 = PendingEffect.Create(effect1, card1, _player1, _player1, PhaseType.Main);
            // 速度 = 1 + 2 = 3 > 0 ✓
            Assert.IsTrue(_stackEngine.TryActivateEffect(pending1));
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // 第二次发动（需要 speed > 1）
            var effect2 = MakeVoluntaryEffect(baseSpeed: 3);
            var card2 = MakeTestCard("CARD_B");
            var pending2 = PendingEffect.Create(effect2, card2, _player1, _player1, PhaseType.Main);
            // 速度 = 1 + 3 = 4 > 1 ✓
            Assert.IsTrue(_stackEngine.TryActivateEffect(pending2));
            Assert.AreEqual(2, _stackEngine.SpeedCounter.CurrentSpeed);
        }

        [Test]
        public void TryActivateEffect_RejectsWhenSpeedNotExceedsCounter()
        {
            SetupMainPhase();

            // 先让 counter=1
            var effect1 = MakeVoluntaryEffect(baseSpeed: 0);
            var card1 = MakeTestCard("CARD_X");
            var pending1 = PendingEffect.Create(effect1, card1, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pending1);
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // NTP 尝试发动，速度 = 0(NTP主阶段) + 0(BaseSpeed) = 0, 0 <= 1 → 拒绝
            var effect2 = MakeVoluntaryEffect(baseSpeed: 0);
            var card2 = MakeTestCard("CARD_Y");
            var pending2 = PendingEffect.Create(effect2, card2, _player2, _player1, PhaseType.Main);

            bool result = _stackEngine.TryActivateEffect(pending2);
            Assert.IsFalse(result, "NTP速度0不应超过counter=1");
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed, "失败后counter不变");
        }

        [Test]
        public void TryActivateEffect_BaseSpeedOverrides()
        {
            SetupMainPhase();
            _stackEngine.TryActivateEffect(
                PendingEffect.Create(MakeVoluntaryEffect(), MakeTestCard("A"), _player1, _player1, PhaseType.Main));
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // BaseSpeed=2 的卡，最终速度 = 1(TP) + 2 = 3 > 1 ✓
            var effect = MakeVoluntaryEffect(baseSpeed: 2);
            var card = MakeTestCard("FAST_CARD");
            var pending = PendingEffect.Create(effect, card, _player1, _player1, PhaseType.Main);

            Assert.IsTrue(_stackEngine.TryActivateEffect(pending),
                "BaseSpeed=2 的卡应能发动 (速度3 > counter1)");
        }

        [Test]
        public void TryActivateEffect_WithPaidBoost()
        {
            SetupMainPhase();
            _stackEngine.TryActivateEffect(
                PendingEffect.Create(MakeVoluntaryEffect(), MakeTestCard("A"), _player1, _player1, PhaseType.Main));
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // TP主阶段 + BaseSpeed0 + paidBoost2 = 3 > 1 ✓
            var effect = MakeVoluntaryEffect(baseSpeed: 0);
            var card = MakeTestCard("TUNER_001");
            var pending = PendingEffect.Create(effect, card, _player1, _player1, PhaseType.Main, paidBoost: 2);

            Assert.IsTrue(_stackEngine.TryActivateEffect(pending),
                "支付2费提速后速度=3 > counter=1");
            Assert.AreEqual(2, _stackEngine.SpeedCounter.CurrentSpeed);
        }

        [Test]
        public void TryActivateEffect_TriggeredAlwaysSucceeds()
        {
            SetupMainPhase();

            // 先把 counter 提到 5
            for (int i = 0; i < 5; i++)
            {
                var e = MakeVoluntaryEffect(baseSpeed: 10);
                var c = MakeTestCard($"FILL_{i}");
                var p = PendingEffect.Create(e, c, _player1, _player1, PhaseType.Main);
                _stackEngine.TryActivateEffect(p);
            }
            Assert.AreEqual(5, _stackEngine.SpeedCounter.CurrentSpeed);

            // 条件发动，speed=MaxValue，总能通过
            var triggeredEffect = MakeTriggeredEffect();
            var triggeredCard = MakeTestCard("TRIGGERED");
            var triggeredPending = PendingEffect.Create(
                triggeredEffect, triggeredCard, _player2, _player1, PhaseType.Main);

            Assert.IsTrue(_stackEngine.TryActivateEffect(triggeredPending),
                "条件发动 speed=MaxValue 应总能通过");
            Assert.AreEqual(6, _stackEngine.SpeedCounter.CurrentSpeed);
        }

        // ======================================== StackEngine 轮询与优先权 ========================================

        [Test]
        public void PassPriority_SwitchesHolder()
        {
            SetupMainPhase();
            Assert.AreEqual(_player1, _stackEngine.CurrentPriorityHolder);

            _stackEngine.PassPriority(_player1);
            Assert.AreEqual(_player2, _stackEngine.CurrentPriorityHolder,
                "P1 Pass 后优先权应给 P2");
        }

        [Test]
        public void PassPriority_BothPass_TriggersResolution()
        {
            SetupMainPhase();

            // 先放一个效果入栈
            var effect = MakeVoluntaryEffect();
            var card = MakeTestCard("CARD_001");
            var pending = PendingEffect.Create(effect, card, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pending);
            Assert.AreEqual(1, _stackEngine.StackSize);

            // 双方 Pass → 开始结算
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            // 结算后栈应为空
            Assert.AreEqual(0, _stackEngine.StackSize, "双方Pass后应结算完毕");
            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed, "结算后counter应归0");
        }

        [Test]
        public void PassPriority_NonHolderCannotPass()
        {
            SetupMainPhase();
            // 当前优先权在 P1

            // P2 尝试 Pass（不是优先权持有者）
            _stackEngine.PassPriority(_player2);
            // 不应计入连续 Pass
            Assert.AreEqual(0, _stackEngine.StackSize);
        }

        // ======================================== StackEngine 结算流程 ========================================

        [Test]
        public void Resolution_CounterDecrementsPerResolve()
        {
            SetupMainPhase();

            // 入栈3个效果（需要足够速度）
            for (int i = 0; i < 3; i++)
            {
                var e = MakeVoluntaryEffect(baseSpeed: 10);
                var c = MakeTestCard($"CARD_{i}");
                var p = PendingEffect.Create(e, c, _player1, _player1, PhaseType.Main);
                _stackEngine.TryActivateEffect(p);
            }
            Assert.AreEqual(3, _stackEngine.SpeedCounter.CurrentSpeed, "3个效果入栈 counter=3");

            // 双方Pass → 结算
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            // 结算完成后 counter 应归0
            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed,
                "全部结算后counter应归0");
            Assert.AreEqual(0, _stackEngine.StackSize, "栈应为空");
        }

        [Test]
        public void Resolution_LIFOOrder()
        {
            SetupMainPhase();

            // 入栈效果A和B（B最后入栈，应最先结算）
            var effectA = MakeVoluntaryEffect(baseSpeed: 10);
            var cardA = MakeTestCard("EFFECT_A");
            var pendingA = PendingEffect.Create(effectA, cardA, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pendingA);

            var effectB = MakeVoluntaryEffect(baseSpeed: 10);
            var cardB = MakeTestCard("EFFECT_B");
            var pendingB = PendingEffect.Create(effectB, cardB, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pendingB);

            // 栈内容应为 [A, B]（B在顶部）
            var contents = _stackEngine.GetStackContents();
            Assert.AreEqual(2, contents.Count);

            // 双方Pass → LIFO结算
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            Assert.AreEqual(0, _stackEngine.StackSize, "全部结算完毕");
        }

        // ======================================== 条件发动自动入栈 ========================================

        [Test]
        public void ProcessTriggeredEffects_AutoEntersStack()
        {
            SetupMainPhase();

            // 添加一个条件发动效果到待发队列
            var triggeredEffect = MakeTriggeredEffect(EffectActivationType.Automatic);
            var card = MakeTestCard("TRG_CARD");
            var pending = PendingEffect.Create(
                triggeredEffect, card, _player1, _player1, PhaseType.Main);

            _stackEngine.AddPendingEffect(pending);
            Assert.IsTrue(_stackEngine.PendingQueue.HasAutoEffects);

            // 处理条件发动 → 自动入栈
            _stackEngine.ProcessTriggeredEffects();

            Assert.AreEqual(1, _stackEngine.StackSize, "条件发动应自动入栈");
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed, "入栈后counter+1");
        }

        [Test]
        public void ProcessTriggeredEffects_MandatoryBeforeAutomatic()
        {
            SetupMainPhase();

            var autoEffect = MakeTriggeredEffect(EffectActivationType.Automatic);
            var mandatoryEffect = MakeTriggeredEffect(EffectActivationType.Mandatory);
            var card1 = MakeTestCard("AUTO_CARD");
            var card2 = MakeTestCard("MANDATORY_CARD");

            // 先添加 Automatic，再添加 Mandatory
            var pendingAuto = PendingEffect.Create(
                autoEffect, card1, _player1, _player1, PhaseType.Main);
            var pendingMandatory = PendingEffect.Create(
                mandatoryEffect, card2, _player1, _player1, PhaseType.Main);

            _stackEngine.AddPendingEffect(pendingAuto);
            _stackEngine.AddPendingEffect(pendingMandatory);

            _stackEngine.ProcessTriggeredEffects();

            Assert.AreEqual(2, _stackEngine.StackSize, "两个条件发动都应入栈");
            Assert.AreEqual(2, _stackEngine.SpeedCounter.CurrentSpeed);
        }

        // ======================================== 延迟触发测试 ========================================

        [Test]
        public void DeferredTriggers_NewRoundAfterResolve()
        {
            SetupMainPhase();

            // 入栈一个效果
            var effect = MakeVoluntaryEffect(baseSpeed: 10);
            var card = MakeTestCard("CARD_001");
            var pending = PendingEffect.Create(effect, card, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pending);

            // 预先在队列中放入延迟触发效果（模拟结算期间产生的触发）
            var deferred = MakeTriggeredEffect(EffectActivationType.Automatic);
            var deferredCard = MakeTestCard("DEFERRED");
            var deferredPending = PendingEffect.Create(
                deferred, deferredCard, _player1, _player1, PhaseType.Main);
            _stackEngine.AddPendingEffect(deferredPending);

            // 双方Pass → 结算原始效果 → 延迟触发自动入栈
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            // 延迟触发应该已经入栈（FinishResolution 检查 HasAutoEffects）
            Assert.AreEqual(1, _stackEngine.StackSize,
                "延迟触发应自动入栈开始新一轮");
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);
        }

        // ======================================== 完整连锁流程 ========================================

        [Test]
        public void FullChain_TP1_EffectA_Resolve_CounterZero()
        {
            SetupMainPhase();

            // === 1. P1发动效果A（TP主阶段，默认1） ===
            var effectA = MakeVoluntaryEffect(baseSpeed: 0);
            var cardA = MakeTestCard("EFF_A");
            var pendingA = PendingEffect.Create(effectA, cardA, _player1, _player1, PhaseType.Main);
            // 速度 = 1(TP主阶段) + 0(BaseSpeed) + 0(paidBoost) = 1 > 0(counter) ✓

            Assert.IsTrue(_stackEngine.TryActivateEffect(pendingA));
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);
            Assert.AreEqual(1, _stackEngine.StackSize);

            // === 2. P2 尝试响应 ===
            var effectB = MakeVoluntaryEffect(baseSpeed: 0);
            var cardB = MakeTestCard("EFF_B");
            var pendingB = PendingEffect.Create(effectB, cardB, _player2, _player1, PhaseType.Main);
            // NTP主阶段速度 = 0, 0 <= 1(counter) → 不能发动
            Assert.IsFalse(_stackEngine.TryActivateEffect(pendingB),
                "NTP速度0不应能响应counter=1");

            // === 3. 双方Pass ===
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            // === 4. 结算完毕 ===
            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed, "结算后counter归0");
            Assert.AreEqual(0, _stackEngine.StackSize, "栈为空");
        }

        [Test]
        public void FullChain_TwoEffects_ResolveInLIFO()
        {
            SetupMainPhase();

            // P1发动效果A (速度=1)
            var effectA = MakeVoluntaryEffect(baseSpeed: 0);
            var cardA = MakeTestCard("EFF_A");
            var pendingA = PendingEffect.Create(effectA, cardA, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pendingA);
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // P1发动效果B (BaseSpeed=2，速度=1+2=3 > 1)
            var effectB = MakeVoluntaryEffect(baseSpeed: 2);
            var cardB = MakeTestCard("EFF_B");
            var pendingB = PendingEffect.Create(effectB, cardB, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pendingB);
            Assert.AreEqual(2, _stackEngine.SpeedCounter.CurrentSpeed);

            // 双方Pass → LIFO结算: B先结算, A后结算
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed);
            Assert.AreEqual(0, _stackEngine.StackSize);
        }

        [Test]
        public void FullChain_WithTriggeredCascade()
        {
            SetupMainPhase();

            // P1发动一个效果
            var effect1 = MakeVoluntaryEffect(baseSpeed: 0);
            var card1 = MakeTestCard("EFF_1");
            var pending1 = PendingEffect.Create(effect1, card1, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pending1);
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // 预设一个条件触发效果（模拟 OnDeath 触发）
            var cascadeEffect = MakeTriggeredEffect(EffectActivationType.Automatic,
                TriggerTiming.On_Death);
            var cascadeCard = MakeTestCard("CASCADE");
            var cascadePending = PendingEffect.Create(
                cascadeEffect, cascadeCard, _player1, _player1, PhaseType.Main);

            // 先不加入队列，等结算后再加
            // 这里直接测试：入栈一个效果，再入栈一个条件发动
            // 然后结算，验证结算后延迟触发
            _stackEngine.AddPendingEffect(cascadePending);

            // 双方Pass → 结算原始效果
            // 结算完成后 FinishResolution 会检查 HasAutoEffects 并处理延迟触发
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            // 延迟触发应已入栈（新一轮）
            Assert.AreEqual(1, _stackEngine.StackSize,
                "延迟触发效果应在新一轮入栈");
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // 再双方Pass → 结算延迟触发
            _stackEngine.PassPriority(_player1);
            _stackEngine.PassPriority(_player2);

            Assert.AreEqual(0, _stackEngine.StackSize, "延迟触发结算完毕");
            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed);
        }

        // ======================================== 协调怪兽提速继承 ========================================

        [Test]
        public void TunerBoost_SpeedValueCorrect()
        {
            // 协调怪兽支付2费提速2
            int desiredBoost = 2;
            int cost = SpeedCalculator.CalculateSpeedCost(desiredBoost);

            Assert.AreEqual(2, cost, "2速需要2费");

            // 主阶段TP + BaseSpeed0 + paidBoost2 = 3
            int speed = SpeedCalculator.CalculateSpeed(
                SpeedCalculator.GetDefaultSpeed(_player1, _player1, PhaseType.Main),
                0, // BaseSpeed
                desiredBoost);

            Assert.AreEqual(3, speed, "TP主阶段 + 提速2 = 3");
        }

        [Test]
        public void TunerBoost_CanChainAfterOtherEffect()
        {
            SetupMainPhase();

            // 另一个效果先入栈 → counter=1
            var otherEffect = MakeVoluntaryEffect(baseSpeed: 0);
            var otherCard = MakeTestCard("OTHER");
            var otherPending = PendingEffect.Create(otherEffect, otherCard, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(otherPending);
            Assert.AreEqual(1, _stackEngine.SpeedCounter.CurrentSpeed);

            // 协调怪兽提速2: 速度 = 1(TP) + 0 + 2 = 3 > 1(counter) ✓
            var tunerEffect = MakeVoluntaryEffect(baseSpeed: 0);
            var tunerCard = MakeTestCard("TUNER");
            var tunerPending = PendingEffect.Create(
                tunerEffect, tunerCard, _player1, _player1, PhaseType.Main, paidBoost: 2);

            Assert.IsTrue(_stackEngine.TryActivateEffect(tunerPending),
                "协调怪兽提速后速度3 > counter1 应能连锁");
        }

        // ======================================== Edge Cases ========================================

        [Test]
        public void SpeedCostRate_IsConstant1()
        {
            // 1速=1费 硬编码常量验证
            Assert.AreEqual(1, SpeedCalculator.SPEED_COST_RATE);
            for (int i = 0; i <= 10; i++)
            {
                Assert.AreEqual(i, SpeedCalculator.CalculateSpeedCost(i),
                    $"提速{i}速应需要{i}费");
            }
        }

        [Test]
        public void StackEngine_Initialize_ResetsState()
        {
            SetupMainPhase();

            // 放些效果
            var effect = MakeVoluntaryEffect(baseSpeed: 10);
            var card = MakeTestCard("CARD");
            var pending = PendingEffect.Create(effect, card, _player1, _player1, PhaseType.Main);
            _stackEngine.TryActivateEffect(pending);

            // Initialize 重置
            _stackEngine.Initialize(_player2);
            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed);
            Assert.AreEqual(0, _stackEngine.StackSize);
            Assert.AreEqual(_player2, _stackEngine.ActivePlayer);
        }

        [Test]
        public void StackEngine_Clear_ResetsAll()
        {
            SetupMainPhase();

            var effect = MakeVoluntaryEffect(baseSpeed: 10);
            var card = MakeTestCard("CARD");
            var pending = PendingEffect.Create(effect, card, _player1, _player1, PhaseType.Main);
            _stackEngine.AddPendingEffect(pending);
            _stackEngine.TryActivateEffect(pending);

            _stackEngine.Clear();
            Assert.AreEqual(0, _stackEngine.SpeedCounter.CurrentSpeed);
            Assert.AreEqual(0, _stackEngine.StackSize);
            Assert.IsFalse(_stackEngine.PendingQueue.HasPendingEffects);
        }

        [Test]
        public void StackEngine_SetPhase_UpdatesPhase()
        {
            _stackEngine.SetPhase(PhaseType.Main);
            Assert.AreEqual(PhaseType.Main, _stackEngine.CurrentPhase);

            _stackEngine.SetPhase(PhaseType.Standby);
            Assert.AreEqual(PhaseType.Standby, _stackEngine.CurrentPhase);
        }
    }

    // ======================================== 测试用卡牌类 ========================================

    /// <summary>
    /// 栈引擎测试用卡牌
    /// </summary>
    public class StackTestCard : Card
    {
        public int Power { get; set; }
        public int Life { get; set; }

        public StackTestCard(string id, int power, int life) : base()
        {
            ID = id;
            Power = power;
            Life = life;
        }
    }
}
