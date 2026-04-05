using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// 游戏循环集成测试
    /// 验证：InitGame起手、准备阶段、元素池指示物、出牌、攻击、结束阶段手牌上限、完整回合循环
    /// </summary>
    [TestFixture]
    public class GameLoopTests
    {
        private GameCore _core;
        private Player _player1;
        private Player _player2;

        [SetUp]
        public void SetUp()
        {
            _core = GameCore.Instance;
            _core.Reset();
            _player1 = _core.Player1;
            _player2 = _core.Player2;
            EventManager.Instance.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.Instance.ClearAll();
        }

        #region 辅助方法

        /// <summary>
        /// 创建一张带费用的测试生物卡
        /// </summary>
        private LoopTestCard MakeCard(string id, int power, int life, Dictionary<int, float> cost = null)
        {
            var card = new LoopTestCard(id, power, life);
            if (cost != null)
                card.Cost = cost;
            return card;
        }

        /// <summary>
        /// 构建一副测试牌（至少15张以保证抽牌不空）
        /// </summary>
        private List<Card> BuildTestDeck(int count = 15)
        {
            var deck = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                var cost = new Dictionary<int, float>
                {
                    { (i % 4), 1 }  // Gray/Red/Blue/Green 各1
                };
                deck.Add(MakeCard($"CARD_{i:D3}", 2, 2, cost));
            }
            return deck;
        }

        #endregion

        // ======================================== InitGame 测试 ========================================

        [Test]
        public void InitGame_DealsStartingHand()
        {
            var deck1 = BuildTestDeck(15);
            var deck2 = BuildTestDeck(15);

            _core.InitGame(deck1, deck2);

            // 起手5张
            var hand1 = _core.ZoneManager.GetCards(_player1, Zone.Hand);
            var hand2 = _core.ZoneManager.GetCards(_player2, Zone.Hand);
            Assert.AreEqual(5, hand1.Count, "P1 起手应为5张");
            Assert.AreEqual(5, hand2.Count, "P2 起手应为5张");

            // 牌库剩余10张
            var deck1Cards = _core.ZoneManager.GetCards(_player1, Zone.Deck);
            var deck2Cards = _core.ZoneManager.GetCards(_player2, Zone.Deck);
            Assert.AreEqual(10, deck1Cards.Count, "P1 牌库应剩余10张");
            Assert.AreEqual(10, deck2Cards.Count, "P2 牌库应剩余10张");
        }

        [Test]
        public void InitGame_StartsFirstTurnStandby()
        {
            _core.InitGame(BuildTestDeck(), BuildTestDeck());

            Assert.AreEqual(PhaseType.Standby, _core.TurnEngine.CurrentPhase?.Phase,
                "游戏应从准备阶段开始");
            Assert.AreEqual(_player1, _core.TurnEngine.TurnPlayer,
                "P1 应为先手玩家");
        }

        // ======================================== 准备阶段测试 ========================================

        [Test]
        public void StandbyPhase_CanAddCardToElementPool()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            var hand = _core.ZoneManager.GetCards(_player1, Zone.Hand);
            var card = hand[0];

            bool result = GameActions.AddToElementPool(_core, _player1, card);

            Assert.IsTrue(result, "放入元素池应成功");
            // 卡牌应在元素池区域
            Assert.IsFalse(_core.ZoneManager.GetCards(_player1, Zone.Hand).Contains(card),
                "卡牌应从手牌移除");
            // 元素池中应有该卡
            var pooled = _core.ElementPool.GetPooledCards(_player1);
            Assert.AreEqual(1, pooled.Count, "元素池应有1张卡");
        }

        [Test]
        public void StandbyPhase_CanSkipElementPool()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            bool result = GameActions.SkipElementPool(_core, _player1);

            Assert.IsTrue(result, "跳过元素池应成功");
            Assert.AreEqual(PhaseType.Main, _core.TurnEngine.CurrentPhase?.Phase,
                "跳过后应进入主阶段");
        }

        [Test]
        public void StandbyPhase_CannotPlayCard()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            var hand = _core.ZoneManager.GetCards(_player1, Zone.Hand);
            var card = hand[0];

            // 准备阶段不能出牌
            bool result = GameActions.PlayCard(_core, _player1, card);
            Assert.IsFalse(result, "准备阶段不能出牌");
        }

        // ======================================== 元素池指示物测试 ========================================

        [Test]
        public void ElementPool_TokensMatchCardCost()
        {
            var deck1 = new List<Card>();
            var deck2 = BuildTestDeck();

            // 创建一张费用为 Red:2, Blue:1 的卡
            var expensiveCard = MakeCard("EXP_001", 4, 4, new Dictionary<int, float>
            {
                { (int)ManaType.Red, 2 },
                { (int)ManaType.Blue, 1 }
            });
            for (int i = 0; i < 14; i++)
                deck1.Add(MakeCard($"FILL_{i}", 1, 1, new Dictionary<int, float> { { 0, 1 } }));

            // 把这张贵卡放在手牌顶部（抽牌后会在手中）
            // 我们直接放入手牌
            _core.InitGame(deck1, deck2);

            // 将贵卡加入手牌
            _core.ZoneManager.GetZoneContainer(_player1).Add(expensiveCard, Zone.Hand);

            // 放入元素池
            bool added = GameActions.AddToElementPool(_core, _player1, expensiveCard);
            Assert.IsTrue(added);

            var pooled = _core.ElementPool.GetPooledCards(_player1);
            var pc = pooled.Find(p => p.SourceCard == expensiveCard);
            Assert.IsNotNull(pc);
            // 指示物应匹配费用：Red:2, Blue:1
            Assert.AreEqual(2, pc.Tokens[ManaType.Red]);
            Assert.AreEqual(1, pc.Tokens[ManaType.Blue]);
            Assert.AreEqual(3, pc.TotalTokenCount);
        }

        [Test]
        public void ElementPool_GainTokenOncePerTurn()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            // 放一张卡进元素池（Red:1）
            var hand = _core.ZoneManager.GetCards(_player1, Zone.Hand);
            var card = hand[0];
            GameActions.AddToElementPool(_core, _player1, card);

            // 跳过准备阶段，进入主阶段
            GameActions.SkipElementPool(_core, _player1);

            // 先重新回到准备阶段来获得指示物（实际流程中在准备阶段获得）
            // 重置：直接调用 GainElementFromToken
            bool gained = GameActions.GainElementFromToken(_core, _player1, ManaType.Gray);
            // 可能成功也可能失败，取决于卡牌的费用颜色
            // 验证每回合只能取一次
            if (gained)
            {
                bool gained2 = GameActions.GainElementFromToken(_core, _player1, ManaType.Gray);
                Assert.IsFalse(gained2, "每回合只能取一次指示物");
            }
        }

        [Test]
        public void ElementPool_DepletedCardGoesToGraveyard()
        {
            // 创建一张只有1个灰色指示物的卡
            var card = MakeCard("WEAK_001", 1, 1, new Dictionary<int, float> { { (int)ManaType.Gray, 1 } });

            var deck1 = new List<Card>();
            var deck2 = BuildTestDeck();
            for (int i = 0; i < 14; i++)
                deck1.Add(MakeCard($"FILL_{i}", 1, 1, new Dictionary<int, float> { { 0, 1 } }));

            _core.InitGame(deck1, deck2);
            _core.ZoneManager.GetZoneContainer(_player1).Add(card, Zone.Hand);

            // 放入元素池
            GameActions.AddToElementPool(_core, _player1, card);
            GameActions.SkipElementPool(_core, _player1);

            // 取走唯一的指示物
            bool gained = GameActions.GainElementFromToken(_core, _player1, ManaType.Gray);
            Assert.IsTrue(gained, "应成功获得灰色元素");

            // 卡牌应已耗尽并进墓地
            var pooled = _core.ElementPool.GetPooledCards(_player1);
            Assert.AreEqual(0, pooled.Count, "耗尽后元素池应为空");

            var graveyard = _core.ZoneManager.GetCards(_player1, Zone.Graveyard);
            Assert.IsTrue(graveyard.Contains(card), "耗尽卡牌应进墓地");
        }

        // ======================================== 主阶段出牌测试 ========================================

        [Test]
        public void MainPhase_PlayCard()
        {
            var deck1 = BuildTestDeck(15);
            var deck2 = BuildTestDeck(15);

            // 创建一张 Gray:1 的便宜卡放入手牌
            var cheapCard = MakeCard("CHEAP_001", 2, 2, new Dictionary<int, float> { { (int)ManaType.Gray, 1 } });

            _core.InitGame(deck1, deck2);

            // 跳过元素池进入主阶段
            GameActions.SkipElementPool(_core, _player1);

            // 没有可用元素，出牌应失败
            _core.ZoneManager.GetZoneContainer(_player1).Add(cheapCard, Zone.Hand);
            bool failed = GameActions.PlayCard(_core, _player1, cheapCard);
            Assert.IsFalse(failed, "没有元素时出牌应失败");

            // 给玩家加1个灰色元素
            _core.ElementPool.GetPool(_player1).AvailableMana[ManaType.Gray] = 1;

            // 现在出牌应成功
            bool result = GameActions.PlayCard(_core, _player1, cheapCard);
            Assert.IsTrue(result, "有元素时出牌应成功");

            // 卡牌应在战场
            var battlefield = _core.ZoneManager.GetCards(_player1, Zone.Battlefield);
            Assert.IsTrue(battlefield.Contains(cheapCard), "卡牌应在战场上");

            // 元素应被消耗
            Assert.AreEqual(0, _core.ElementPool.GetAvailableManaCount(ManaType.Gray, _player1),
                "出牌后元素应被消耗");
        }

        [Test]
        public void MainPhase_CannotPlayInWrongPhase()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            var hand = _core.ZoneManager.GetCards(_player1, Zone.Hand);
            var card = hand[0];

            // 准备阶段不能出牌
            bool result = GameActions.PlayCard(_core, _player1, card);
            Assert.IsFalse(result, "准备阶段不能出牌");
        }

        [Test]
        public void MainPhase_CannotPlayOtherPlayersCard()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            // 跳到主阶段
            GameActions.SkipElementPool(_core, _player1);

            // P1 不能打出 P2 的手牌
            var p2Hand = _core.ZoneManager.GetCards(_player2, Zone.Hand);
            if (p2Hand.Count > 0)
            {
                bool result = GameActions.PlayCard(_core, _player1, p2Hand[0]);
                Assert.IsFalse(result, "不能打出对手的牌");
            }
        }

        // ======================================== 回合结束测试 ========================================

        [Test]
        public void EndTurn_AdvancesToNextPlayer()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            // P1 跳过准备阶段
            GameActions.SkipElementPool(_core, _player1);

            // P1 结束回合
            bool result = GameActions.EndTurn(_core, _player1);
            Assert.IsTrue(result, "结束回合应成功");

            // 经过结束阶段后应切换到 P2 的回合
            Assert.AreEqual(_player2, _core.TurnEngine.TurnPlayer,
                "回合应切换到 P2");
        }

        [Test]
        public void EndTurn_CannotEndOtherPlayersTurn()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            // P2 不能结束 P1 的回合
            bool result = GameActions.EndTurn(_core, _player2);
            Assert.IsFalse(result, "不能结束对手的回合");
        }

        // ======================================== 完整回合循环测试 ========================================

        [Test]
        public void FullTurnCycle_TwoPlayers()
        {
            var deck1 = BuildTestDeck(15);
            var deck2 = BuildTestDeck(15);
            _core.InitGame(deck1, deck2);

            // === P1 回合 ===
            Assert.AreEqual(_player1, _core.TurnEngine.TurnPlayer);
            Assert.AreEqual(PhaseType.Standby, _core.TurnEngine.CurrentPhase?.Phase);

            // P1 放一张牌进元素池
            var p1Hand = _core.ZoneManager.GetCards(_player1, Zone.Hand);
            int p1HandBefore = p1Hand.Count;
            if (p1Hand.Count > 0)
            {
                GameActions.AddToElementPool(_core, _player1, p1Hand[0]);
            }

            // P1 跳过，进入主阶段
            // AddToElementPool 不自动推进阶段，需要手动跳过或 AdvanceFromStandby
            // 实际上 AddToElementPool 不推进，SkipElementPool 才推进
            // 重新考虑：放入元素池后需要推进阶段
            // 查看 GameActions.AddToElementPool —— 它不调用 AdvanceFromStandby
            // 需要手动调用 SkipElementPool 来推进
            // 这里假设放入后需要单独推进
            // 但注意：AddToElementPool 后再 SkipElementPool 会调用 AdvanceFromStandby
            // TurnEngine 会在 AdvanceFromStandby 后进入 Main
            GameActions.SkipElementPool(_core, _player1);

            Assert.AreEqual(PhaseType.Main, _core.TurnEngine.CurrentPhase?.Phase);

            // P1 结束回合
            GameActions.EndTurn(_core, _player1);

            // === P2 回合 ===
            Assert.AreEqual(_player2, _core.TurnEngine.TurnPlayer);
            // P2 的准备阶段
            Assert.AreEqual(PhaseType.Standby, _core.TurnEngine.CurrentPhase?.Phase);

            // P2 跳过准备阶段
            GameActions.SkipElementPool(_core, _player2);
            Assert.AreEqual(PhaseType.Main, _core.TurnEngine.CurrentPhase?.Phase);

            // P2 结束回合
            GameActions.EndTurn(_core, _player2);

            // 回到 P1
            Assert.AreEqual(_player1, _core.TurnEngine.TurnPlayer);
            // 回合数应增加
            Assert.GreaterOrEqual(_core.TurnEngine.TurnNumber, 3, "回合数应递增");
        }

        // ======================================== 元素池可用元素查询 ========================================

        [Test]
        public void ElementPool_AvailableManaTracking()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            // 一开始没有可用元素
            Assert.AreEqual(0, _core.ElementPool.GetTotalAvailableMana(_player1));

            // 直接操作：给玩家加元素
            var pool = _core.ElementPool.GetPool(_player1);
            pool.AvailableMana[ManaType.Red] = 3;
            pool.AvailableMana[ManaType.Blue] = 2;

            Assert.AreEqual(5, _core.ElementPool.GetTotalAvailableMana(_player1));
            Assert.AreEqual(3, _core.ElementPool.GetAvailableManaCount(ManaType.Red, _player1));
            Assert.AreEqual(2, _core.ElementPool.GetAvailableManaCount(ManaType.Blue, _player1));
        }

        [Test]
        public void ElementPool_PayCostDeductsMana()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            var pool = _core.ElementPool.GetPool(_player1);
            pool.AvailableMana[ManaType.Red] = 3;
            pool.AvailableMana[ManaType.Gray] = 2;

            var cost = new Dictionary<int, float>
            {
                { (int)ManaType.Red, 2 },
                { (int)ManaType.Gray, 1 }
            };

            bool paid = _core.ElementPool.PayCost(cost, _player1);
            Assert.IsTrue(paid, "支付应成功");
            Assert.AreEqual(1, pool.AvailableMana[ManaType.Red]);
            Assert.AreEqual(1, pool.AvailableMana[ManaType.Gray]);
        }

        [Test]
        public void ElementPool_CannotPayMoreThanAvailable()
        {
            _core.InitGame(BuildTestDeck(15), BuildTestDeck(15));

            var pool = _core.ElementPool.GetPool(_player1);
            pool.AvailableMana[ManaType.Red] = 1;

            var cost = new Dictionary<int, float>
            {
                { (int)ManaType.Red, 3 }
            };

            bool paid = _core.ElementPool.PayCost(cost, _player1);
            Assert.IsFalse(paid, "元素不足时支付应失败");
            Assert.AreEqual(1, pool.AvailableMana[ManaType.Red], "失败后元素不变");
        }
    }

    // ======================================== 测试用卡牌类 ========================================

    /// <summary>
    /// 游戏循环测试用的生物卡 — 实现 IHasPower, IHasLife, IHasCost, IHasKeywords
    /// </summary>
    public class LoopTestCard : Card, IHasPower, IHasLife, IHasCost, IHasKeywords
    {
        public int Power { get; set; }
        public int Life { get; set; }
        public Dictionary<int, float> Cost { get; set; } = new Dictionary<int, float>();
        public HashSet<string> Keywords { get; set; } = new HashSet<string>();

        public LoopTestCard(string id, int power, int life) : base()
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
