using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// ZoneManager 单元测试
    /// 测试区域管理器的卡牌移动、查询功能
    /// </summary>
    [TestFixture]
    public class ZoneManagerTests
    {
        private ZoneManager _zoneManager;
        private Player _player1;
        private Player _player2;

        [SetUp]
        public void SetUp()
        {
            _zoneManager = new ZoneManager();
            _player1 = new Player("P1", 20);
            _player2 = new Player("P2", 20);
            _player1.Opponent = _player2;
            _player2.Opponent = _player1;

            _zoneManager.InitializePlayer(_player1);
            _zoneManager.InitializePlayer(_player2);
        }

        #region 初始化测试

        [Test]
        public void InitializePlayer_CreatesZoneContainer()
        {
            var container = _zoneManager.GetZoneContainer(_player1);
            Assert.IsNotNull(container);
            Assert.AreEqual(_player1, container.Owner);
        }

        [Test]
        public void InitializePlayer_Idempotent_DoesNotDuplicate()
        {
            _zoneManager.InitializePlayer(_player1);
            _zoneManager.InitializePlayer(_player1);
            var container = _zoneManager.GetZoneContainer(_player1);
            Assert.IsNotNull(container);
        }

        #endregion

        #region 卡牌移动测试

        [Test]
        public void MoveCard_FromDeckToHand_CardMoved()
        {
            var card = new Card { ID = "test-1" };
            var container = _zoneManager.GetZoneContainer(_player1);

            // 先添加到 Deck
            container.Add(card, Zone.Deck);
            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Deck));

            // 移动到 Hand
            _zoneManager.MoveCard(card, _player1, Zone.Deck, Zone.Hand);

            Assert.IsFalse(_zoneManager.IsCardInZone(card, _player1, Zone.Deck));
            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Hand));
        }

        [Test]
        public void MoveCard_FromHandToBattlefield_CardMoved()
        {
            var card = new Card { ID = "test-2" };
            var container = _zoneManager.GetZoneContainer(_player1);

            container.Add(card, Zone.Hand);
            _zoneManager.MoveCard(card, _player1, Zone.Hand, Zone.Battlefield);

            Assert.IsFalse(_zoneManager.IsCardInZone(card, _player1, Zone.Hand));
            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Battlefield));
        }

        [Test]
        public void MoveCard_FromBattlefieldToGraveyard_CardMoved()
        {
            var card = new Card { ID = "test-3" };
            var container = _zoneManager.GetZoneContainer(_player1);

            container.Add(card, Zone.Battlefield);
            _zoneManager.MoveCard(card, _player1, Zone.Battlefield, Zone.Graveyard);

            Assert.IsFalse(_zoneManager.IsCardInZone(card, _player1, Zone.Battlefield));
            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Graveyard));
        }

        [Test]
        public void MoveCard_CardNotInSourceZone_StillAddsToTarget()
        {
            var card = new Card { ID = "test-4" };

            // 卡牌不在任何区域，仍应添加到目标区域
            _zoneManager.MoveCard(card, _player1, Zone.Deck, Zone.Hand);

            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Hand));
        }

        #endregion

        #region 查询测试

        [Test]
        public void GetCards_ReturnsAllCardsInZone()
        {
            var card1 = new Card { ID = "c1" };
            var card2 = new Card { ID = "c2" };
            var card3 = new Card { ID = "c3" };

            var container = _zoneManager.GetZoneContainer(_player1);
            container.Add(card1, Zone.Hand);
            container.Add(card2, Zone.Hand);
            container.Add(card3, Zone.Battlefield);

            var handCards = _zoneManager.GetCards(_player1, Zone.Hand);
            Assert.AreEqual(2, handCards.Count);
            Assert.IsTrue(handCards.Any(c => c.ID == "c1"));
            Assert.IsTrue(handCards.Any(c => c.ID == "c2"));
        }

        [Test]
        public void IsCardInZone_CardNotPresent_ReturnsFalse()
        {
            var card = new Card { ID = "missing" };
            Assert.IsFalse(_zoneManager.IsCardInZone(card, _player1, Zone.Hand));
        }

        [Test]
        public void GetCards_EmptyZone_ReturnsEmptyList()
        {
            var cards = _zoneManager.GetCards(_player1, Zone.Graveyard);
            Assert.IsNotNull(cards);
            Assert.AreEqual(0, cards.Count);
        }

        [Test]
        public void GetCount_ReturnsCorrectCount()
        {
            var container = _zoneManager.GetZoneContainer(_player1);
            container.Add(new Card { ID = "a" }, Zone.Deck);
            container.Add(new Card { ID = "b" }, Zone.Deck);
            container.Add(new Card { ID = "c" }, Zone.Deck);

            Assert.AreEqual(3, container.GetCount(Zone.Deck));
        }

        #endregion

        #region 扩展方法测试

        [Test]
        public void DrawCard_FromDeckToHand_ReturnsCard()
        {
            var card = new Card { ID = "draw-1" };
            var container = _zoneManager.GetZoneContainer(_player1);
            container.Add(card, Zone.Deck);

            var drawn = _zoneManager.DrawCard(_player1);
            Assert.IsNotNull(drawn);
            Assert.AreEqual("draw-1", drawn.ID);
            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Hand));
            Assert.IsFalse(_zoneManager.IsCardInZone(card, _player1, Zone.Deck));
        }

        [Test]
        public void DrawCard_EmptyDeck_ReturnsNull()
        {
            var drawn = _zoneManager.DrawCard(_player1);
            Assert.IsNull(drawn);
        }

        [Test]
        public void MillCard_FromDeckToGraveyard_ReturnsCard()
        {
            var card = new Card { ID = "mill-1" };
            var container = _zoneManager.GetZoneContainer(_player1);
            container.Add(card, Zone.Deck);

            var milled = _zoneManager.MillCard(_player1);
            Assert.IsNotNull(milled);
            Assert.AreEqual("mill-1", milled.ID);
            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Graveyard));
        }

        [Test]
        public void GetTopCards_ReturnsCorrectCount()
        {
            var container = _zoneManager.GetZoneContainer(_player1);
            for (int i = 0; i < 5; i++)
                container.Add(new Card { ID = $"top-{i}" }, Zone.Deck);

            var top3 = _zoneManager.GetTopCards(_player1, 3);
            Assert.AreEqual(3, top3.Count);
        }

        [Test]
        public void GetTopCards_RequestMoreThanAvailable_ReturnsAllAvailable()
        {
            var container = _zoneManager.GetZoneContainer(_player1);
            container.Add(new Card { ID = "only" }, Zone.Deck);

            var top = _zoneManager.GetTopCards(_player1, 10);
            Assert.AreEqual(1, top.Count);
        }

        #endregion

        #region 玩家隔离测试

        [Test]
        public void Cards_Player1Zone_DoesNotAppearInPlayer2Zone()
        {
            var card = new Card { ID = "p1-card" };
            var container = _zoneManager.GetZoneContainer(_player1);
            container.Add(card, Zone.Hand);

            Assert.IsTrue(_zoneManager.IsCardInZone(card, _player1, Zone.Hand));
            Assert.IsFalse(_zoneManager.IsCardInZone(card, _player2, Zone.Hand));
        }

        [Test]
        public void Clear_OnlyAffectsTargetZone()
        {
            var container = _zoneManager.GetZoneContainer(_player1);
            container.Add(new Card { ID = "h1" }, Zone.Hand);
            container.Add(new Card { ID = "h2" }, Zone.Hand);
            container.Add(new Card { ID = "b1" }, Zone.Battlefield);

            container.Clear(Zone.Hand);

            Assert.AreEqual(0, container.GetCount(Zone.Hand));
            Assert.AreEqual(1, container.GetCount(Zone.Battlefield));
        }

        #endregion
    }
}
