using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// EventManager 单元测试
    /// 测试事件订阅、发布、取消订阅、清理等功能
    /// </summary>
    [TestFixture]
    public class EventManagerTests
    {
        private EventManager _eventManager;

        [SetUp]
        public void SetUp()
        {
            // 重置单例
            _eventManager = EventManager.Instance;
            _eventManager.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            _eventManager.ClearAll();
        }

        #region 订阅/发布基础测试

        [Test]
        public void Publish_NoSubscribers_ReturnsFalse()
        {
            var evt = new TestEventData();
            bool result = _eventManager.Publish(evt);
            Assert.IsFalse(result);
        }

        [Test]
        public void Publish_WithSubscriber_ReturnsTrue()
        {
            var evt = new TestEventData();
            _eventManager.Subscribe<TestEventData>(e => true);
            bool result = _eventManager.Publish(evt);
            Assert.IsTrue(result);
        }

        [Test]
        public void Publish_SubscriberReceivesEvent()
        {
            TestEventData received = null;
            _eventManager.Subscribe<TestEventData>(e => { received = e; return true; });

            var evt = new TestEventData { Value = 42 };
            _eventManager.Publish(evt);

            Assert.IsNotNull(received);
            Assert.AreEqual(42, received.Value);
        }

        [Test]
        public void Publish_MultipleSubscribers_AllReceive()
        {
            int count = 0;
            _eventManager.Subscribe<TestEventData>(e => { count++; return true; });
            _eventManager.Subscribe<TestEventData>(e => { count++; return true; });

            _eventManager.Publish(new TestEventData());
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Publish_SubscriberReturnsFalse_PublishReturnsFalse()
        {
            _eventManager.Subscribe<TestEventData>(e => false);
            bool result = _eventManager.Publish(new TestEventData());
            Assert.IsFalse(result);
        }

        [Test]
        public void Publish_SubscriberThrows_PublishReturnsFalse()
        {
            _eventManager.Subscribe<TestEventData>(e => throw new Exception("Test exception"));
            bool result = _eventManager.Publish(new TestEventData());
            Assert.IsFalse(result);
        }

        #endregion

        #region Action 订阅测试

        [Test]
        public void Subscribe_ActionVersion_Works()
        {
            TestEventData received = null;
            _eventManager.Subscribe<TestEventData>(e => received = e);

            var evt = new TestEventData { Value = 99 };
            _eventManager.Publish(evt);

            Assert.IsNotNull(received);
            Assert.AreEqual(99, received.Value);
        }

        #endregion

        #region 取消订阅测试

        [Test]
        public void Unsubscribe_NoLongerReceivesEvents()
        {
            int count = 0;
            Func<TestEventData, bool> handler = e => { count++; return true; };

            _eventManager.Subscribe(handler);
            _eventManager.Publish(new TestEventData());
            Assert.AreEqual(1, count);

            _eventManager.Unsubscribe(handler);
            _eventManager.Publish(new TestEventData());
            Assert.AreEqual(1, count); // Should not increment
        }

        [Test]
        public void Unsubscribe_ActionVersion_NoLongerReceivesEvents()
        {
            int count = 0;
            Action<TestEventData> handler = e => count++;

            _eventManager.Subscribe(handler);
            _eventManager.Publish(new TestEventData());
            Assert.AreEqual(1, count);

            _eventManager.Unsubscribe(handler);
            _eventManager.Publish(new TestEventData());
            Assert.AreEqual(1, count);
        }

        #endregion

        #region 定向事件测试

        [Test]
        public void Publish_Targeted_ReachesOnlyTarget()
        {
            int count = 0;
            _eventManager.Subscribe<TestEventData>(e => { count++; return true; }, receiverId: 1);
            _eventManager.Subscribe<TestEventData>(e => { count++; return true; }, receiverId: 2);

            _eventManager.Publish(new TestEventData(), targetId: 1);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Publish_Targeted_NoSubscribers_ReturnsFalse()
        {
            _eventManager.Subscribe<TestEventData>(e => true, receiverId: 1);
            bool result = _eventManager.Publish(new TestEventData(), targetId: 999);
            Assert.IsFalse(result);
        }

        #endregion

        #region 清理测试

        [Test]
        public void ClearAll_RemovesAllSubscriptions()
        {
            _eventManager.Subscribe<TestEventData>(e => true);
            _eventManager.Subscribe<AnotherTestData>(e => true);

            Assert.Greater(_eventManager.ObserverCount, 0);
            _eventManager.ClearAll();
            Assert.AreEqual(0, _eventManager.ObserverCount);
        }

        [Test]
        public void ClearAll_PublishReturnsFalse()
        {
            _eventManager.Subscribe<TestEventData>(e => true);
            _eventManager.ClearAll();
            bool result = _eventManager.Publish(new TestEventData());
            Assert.IsFalse(result);
        }

        [Test]
        public void ObserverCount_ReflectsSubscriptionCount()
        {
            int initial = _eventManager.ObserverCount;
            _eventManager.Subscribe<TestEventData>(e => true);
            Assert.AreEqual(initial + 1, _eventManager.ObserverCount);
        }

        #endregion

        #region 类型隔离测试

        [Test]
        public void Publish_DifferentEventTypes_DoNotCross()
        {
            bool testReceived = false;
            bool anotherReceived = false;

            _eventManager.Subscribe<TestEventData>(e => { testReceived = true; return true; });
            _eventManager.Subscribe<AnotherTestData>(e => { anotherReceived = true; return true; });

            _eventManager.Publish(new TestEventData());
            Assert.IsTrue(testReceived);
            Assert.IsFalse(anotherReceived);
        }

        [Test]
        public void Publish_GameEvent_PolymorphicSubscription()
        {
            // IGameEvent subscription should NOT receive events when subscribing to a different type
            bool received = false;
            _eventManager.Subscribe<CardDestroyEvent>(e => { received = true; return true; });

            _eventManager.Publish(new CardPlayEvent());
            Assert.IsFalse(received);
        }

        #endregion

        #region 测试用事件类型

        private class TestEventData : IEventData
        {
            public int Value { get; set; }
        }

        private class AnotherTestData : IEventData
        {
            public string Text { get; set; }
        }

        #endregion
    }
}
