using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// CombatSystem 单元测试
    /// 测试战斗流程：攻击宣言、阻挡宣言、伤害计算和结算
    /// </summary>
    [TestFixture]
    public class CombatSystemTests
    {
        private CombatSystem _combatSystem;
        private ZoneManager _zoneManager;
        private Player _attacker;
        private Player _defender;
        private List<IGameEvent> _publishedEvents;

        [SetUp]
        public void SetUp()
        {
            _zoneManager = new ZoneManager();
            _attacker = new Player("Attacker", 20);
            _defender = new Player("Defender", 20);
            _attacker.Opponent = _defender;
            _defender.Opponent = _attacker;

            _zoneManager.InitializePlayer(_attacker);
            _zoneManager.InitializePlayer(_defender);

            _combatSystem = new CombatSystem(_zoneManager);

            _publishedEvents = new List<IGameEvent>();
            EventManager.Instance.ClearAll();
            EventManager.Instance.Subscribe<IGameEvent>(e => _publishedEvents.Add(e));
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.Instance.ClearAll();
        }

        #region 战斗启动测试

        [Test]
        public void StartCombat_SetsPhaseToSelectAttacker()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            Assert.AreEqual(CombatPhase.SelectAttacker, _combatSystem.CurrentPhase);
        }

        [Test]
        public void StartCombat_SetsPlayers()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            Assert.AreEqual(_attacker, _combatSystem.AttackingPlayer);
            Assert.AreEqual(_defender, _combatSystem.DefendingPlayer);
        }

        [Test]
        public void StartCombat_ClearsPreviousState()
        {
            // Start and end a combat first
            _combatSystem.StartCombat(_attacker, _defender);
            _combatSystem.EndCombat();

            // Start new combat
            _combatSystem.StartCombat(_attacker, _defender);
            Assert.AreEqual(0, _combatSystem.Attackers.Count);
            Assert.AreEqual(0, _combatSystem.Blockers.Count);
        }

        [Test]
        public void StartCombat_PublishesCombatPhaseStartEvent()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            var evt = _publishedEvents.OfType<CombatPhaseStartEvent>().FirstOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(_attacker, evt.AttackingPlayer);
            Assert.AreEqual(_defender, evt.DefendingPlayer);
        }

        [Test]
        public void InCombat_WhenStarted_ReturnsTrue()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            Assert.IsTrue(_combatSystem.InCombat);
        }

        [Test]
        public void InCombat_WhenNotStarted_ReturnsFalse()
        {
            Assert.IsFalse(_combatSystem.InCombat);
        }

        #endregion

        #region 攻击宣言测试

        [Test]
        public void CanDeclareAttack_ValidCreature_ReturnsTrue()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            Assert.IsTrue(_combatSystem.CanDeclareAttack(creature, _attacker));
        }

        [Test]
        public void CanDeclareAttack_ZeroPowerCreature_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 0, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            Assert.IsFalse(_combatSystem.CanDeclareAttack(creature, _attacker));
        }

        [Test]
        public void CanDeclareAttack_TappedCreature_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            creature.IsTapped = true;
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            Assert.IsFalse(_combatSystem.CanDeclareAttack(creature, _attacker));
        }

        [Test]
        public void CanDeclareAttack_DefenderCreature_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_defender).Add(creature, Zone.Battlefield);

            Assert.IsFalse(_combatSystem.CanDeclareAttack(creature, _defender));
        }

        [Test]
        public void CanDeclareAttack_NotOnBattlefield_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            // Not added to battlefield

            Assert.IsFalse(_combatSystem.CanDeclareAttack(creature, _attacker));
        }

        [Test]
        public void CanDeclareAttack_AlreadyAttacking_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            var target = new TestEntity();
            _combatSystem.DeclareAttack(creature, target);

            Assert.IsFalse(_combatSystem.CanDeclareAttack(creature, _attacker));
        }

        [Test]
        public void CanDeclareAttack_NotInCombatPhase_ReturnsFalse()
        {
            var creature = new TestCreatureCard("c1", 3, 3);
            Assert.IsFalse(_combatSystem.CanDeclareAttack(creature, _attacker));
        }

        [Test]
        public void CanDeclareAttack_WrongPlayer_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            Assert.IsFalse(_combatSystem.CanDeclareAttack(creature, _defender));
        }

        [Test]
        public void DeclareAttack_AddsToAttackerList()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            _combatSystem.DeclareAttack(creature, _defender);

            Assert.AreEqual(1, _combatSystem.Attackers.Count);
            Assert.AreEqual(creature, _combatSystem.Attackers[0].Entity);
        }

        [Test]
        public void DeclareAttack_TapsCreature()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            _combatSystem.DeclareAttack(creature, _defender);

            Assert.IsTrue(creature.IsTapped);
        }

        [Test]
        public void DeclareAttack_SetsPhaseToDeclareAttack()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            _combatSystem.DeclareAttack(creature, _defender);

            Assert.AreEqual(CombatPhase.DeclareAttack, _combatSystem.CurrentPhase);
        }

        [Test]
        public void DeclareAttack_PublishesAttackDeclarationEvent()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("c1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            _combatSystem.DeclareAttack(creature, _defender);

            var evt = _publishedEvents.OfType<AttackDeclarationEvent>().FirstOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(creature, evt.Attacker);
        }

        #endregion

        #region 阻挡宣言测试

        [Test]
        public void CanBlock_ValidBlock_ReturnsTrue()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            var blocker = new TestCreatureCard("b1", 2, 4);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();

            Assert.IsTrue(_combatSystem.CanBlock(blocker, attacker, _defender));
        }

        [Test]
        public void CanBlock_TappedBlocker_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            var blocker = new TestCreatureCard("b1", 2, 4);
            blocker.IsTapped = true;
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();

            Assert.IsFalse(_combatSystem.CanBlock(blocker, attacker, _defender));
        }

        [Test]
        public void CanBlock_AlreadyBlocking_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            var blocker = new TestCreatureCard("b1", 2, 4);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();
            _combatSystem.DeclareBlock(blocker, attacker);

            Assert.IsFalse(_combatSystem.CanBlock(blocker, attacker, _defender));
        }

        [Test]
        public void CanBlock_AttackerNotInList_ReturnsFalse()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            var blocker = new TestCreatureCard("b1", 2, 4);
            var wrongAttacker = new TestCreatureCard("a2", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();

            Assert.IsFalse(_combatSystem.CanBlock(blocker, wrongAttacker, _defender));
        }

        [Test]
        public void DeclareBlock_SetsPhaseToDeclareBlock()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            var blocker = new TestCreatureCard("b1", 2, 4);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();
            _combatSystem.DeclareBlock(blocker, attacker);

            Assert.AreEqual(CombatPhase.DeclareBlock, _combatSystem.CurrentPhase);
        }

        [Test]
        public void DeclareBlock_PublishesBlockDeclarationEvent()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            var blocker = new TestCreatureCard("b1", 2, 4);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();
            _combatSystem.DeclareBlock(blocker, attacker);

            var evt = _publishedEvents.OfType<BlockDeclarationEvent>().FirstOrDefault();
            Assert.IsNotNull(evt);
            Assert.AreEqual(blocker, evt.Blocker);
            Assert.AreEqual(attacker, evt.Attacker);
        }

        #endregion

        #region 伤害计算测试

        [Test]
        public void EndAttackDeclaration_NoAttackers_EndsCombat()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            _combatSystem.EndAttackDeclaration();

            Assert.AreEqual(CombatPhase.None, _combatSystem.CurrentPhase);
        }

        [Test]
        public void EndBlockDeclaration_ProceedsToDamageCalculation()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();
            _combatSystem.EndBlockDeclaration();

            Assert.AreEqual(CombatPhase.DamageDealing, _combatSystem.CurrentPhase);
        }

        [Test]
        public void UnblockedAttack_DamagesPlayer()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("a1", 5, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            _combatSystem.DeclareAttack(creature, _defender);
            _combatSystem.EndAttackDeclaration();
            // No blockers
            _combatSystem.EndBlockDeclaration();

            // Combat should be over, check life
            var lifeChange = _publishedEvents.OfType<LifeChangeEvent>().FirstOrDefault();
            Assert.IsNotNull(lifeChange);
            Assert.AreEqual(20, lifeChange.OldLife);
            Assert.AreEqual(15, lifeChange.NewLife);
            Assert.AreEqual(15, _defender.Life);
        }

        [Test]
        public void BlockedAttack_DamagesBothCreatures()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 3, 3);
            var blocker = new TestCreatureCard("b1", 2, 4);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();
            _combatSystem.DeclareBlock(blocker, attacker);
            _combatSystem.EndBlockDeclaration();

            // Attacker takes 2 damage (blocker power)
            Assert.AreEqual(1, attacker.Life); // 3 - 2 = 1
            // Blocker takes 3 damage (attacker power)
            Assert.AreEqual(1, blocker.Life); // 4 - 3 = 1
            // Player should not take damage
            Assert.AreEqual(20, _defender.Life);
        }

        #endregion

        #region 战斗结束测试

        [Test]
        public void EndCombat_SetsPhaseToNone()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            _combatSystem.EndCombat();

            Assert.AreEqual(CombatPhase.None, _combatSystem.CurrentPhase);
        }

        [Test]
        public void EndCombat_ClearsAttackersAndBlockers()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("a1", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);
            _combatSystem.DeclareAttack(creature, _defender);

            _combatSystem.EndCombat();

            Assert.AreEqual(0, _combatSystem.Attackers.Count);
            Assert.AreEqual(0, _combatSystem.Blockers.Count);
        }

        [Test]
        public void EndCombat_PublishesEndEvent()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            _combatSystem.EndCombat();

            var evt = _publishedEvents.OfType<CombatPhaseEndEvent>().FirstOrDefault();
            Assert.IsNotNull(evt);
        }

        [Test]
        public void CancelCombat_ResetsToNone()
        {
            _combatSystem.StartCombat(_attacker, _defender);
            _combatSystem.CancelCombat();

            Assert.AreEqual(CombatPhase.None, _combatSystem.CurrentPhase);
        }

        #endregion

        #region 完整战斗流程测试

        [Test]
        public void FullCombatFlow_UnblockedAttack()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var creature = new TestCreatureCard("a1", 4, 4);
            _zoneManager.GetZoneContainer(_attacker).Add(creature, Zone.Battlefield);

            // Phase 1: Declare attack
            _combatSystem.DeclareAttack(creature, _defender);
            Assert.AreEqual(CombatPhase.DeclareAttack, _combatSystem.CurrentPhase);

            // Phase 2: End attack declaration
            _combatSystem.EndAttackDeclaration();
            Assert.AreEqual(CombatPhase.SelectBlocker, _combatSystem.CurrentPhase);

            // Phase 3: No blocks, end block declaration
            _combatSystem.EndBlockDeclaration();

            // Verify damage
            Assert.AreEqual(16, _defender.Life);
            Assert.AreEqual(CombatPhase.None, _combatSystem.CurrentPhase); // Combat ended
        }

        [Test]
        public void FullCombatFlow_BlockedAttack()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var attacker = new TestCreatureCard("a1", 4, 2);
            var blocker = new TestCreatureCard("b1", 1, 5);
            _zoneManager.GetZoneContainer(_attacker).Add(attacker, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_defender).Add(blocker, Zone.Battlefield);

            _combatSystem.DeclareAttack(attacker, _defender);
            _combatSystem.EndAttackDeclaration();
            _combatSystem.DeclareBlock(blocker, attacker);
            _combatSystem.EndBlockDeclaration();

            // Attacker takes 1 damage (blocker power): 2 - 1 = 1
            Assert.AreEqual(1, attacker.Life);
            // Blocker takes 4 damage (attacker power): 5 - 4 = 1
            Assert.AreEqual(1, blocker.Life);
            // Defender takes no damage
            Assert.AreEqual(20, _defender.Life);
        }

        [Test]
        public void FullCombatFlow_MultipleAttackers()
        {
            _combatSystem.StartCombat(_attacker, _defender);

            var a1 = new TestCreatureCard("a1", 2, 2);
            var a2 = new TestCreatureCard("a2", 3, 3);
            _zoneManager.GetZoneContainer(_attacker).Add(a1, Zone.Battlefield);
            _zoneManager.GetZoneContainer(_attacker).Add(a2, Zone.Battlefield);

            _combatSystem.DeclareAttack(a1, _defender);
            _combatSystem.DeclareAttack(a2, _defender);
            _combatSystem.EndAttackDeclaration();
            _combatSystem.EndBlockDeclaration();

            // Both deal damage: 2 + 3 = 5
            Assert.AreEqual(15, _defender.Life);
        }

        #endregion
    }
}
