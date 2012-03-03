using System;
using System.Reflection;
using NAct;
using NAct.Utils;
using NUnit.Framework;

namespace NActTests.UnitTests
{
    [TestFixture]
    public class ProxyFactoryTest
    {
        private bool m_InvokeHappenedCalled = false;

        private static bool s_MyStaticMethodCalled = false;
        private bool m_MyInstanceMethodCalled = false;

        private static readonly MethodInfo s_MyStaticMethodInfo = typeof(ProxyFactoryTest).GetMethod("MyStaticMethod");
        private static readonly MethodInfo s_MyInstanceMethodInfo = typeof(ProxyFactoryTest).GetMethod("MyInstanceMethod");
        private static readonly MethodInfo s_MyReturningInstanceMethodInfo = typeof(ProxyFactoryTest).GetMethod("MyReturningInstanceMethod");
        private static readonly Type s_MyDelegateType = typeof(MyDelegate);
        private static readonly Type s_MyReturningDelegateType = typeof(MyReturningDelegate);

        [Test]
        public void TestStaticDelegate()
        {
            m_InvokeHappenedCalled = false;
            s_MyStaticMethodCalled = false;

            // Make the proxy method
            MyDelegate proxyDelegate =
                (MyDelegate)new ProxyFactory().CreateDelegateProxy(new MyInvocationHandler(this), s_MyStaticMethodInfo, typeof(MyDelegate));

            EventToSignUpTo += proxyDelegate;

            InvokeEventToSignUpTo("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
            Assert.IsFalse(s_MyStaticMethodCalled);
        }

        [Test]
        public void TestInstanceDelegate()
        {
            m_InvokeHappenedCalled = false;
            m_MyInstanceMethodCalled = false;

            // Make the proxy method
            MyDelegate proxyDelegate =
                (MyDelegate)new ProxyFactory().CreateDelegateProxy(new MyInvocationHandler(this), s_MyInstanceMethodInfo, typeof(MyDelegate));

            EventToSignUpTo += proxyDelegate;

            InvokeEventToSignUpTo("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
            Assert.IsFalse(m_MyInstanceMethodCalled);
        }

        [Test]
        public void TestInterface()
        {
            IInterfaceToFake fakeObject = (IInterfaceToFake)new ProxyFactory().CreateInterfaceProxy(new MyInterfaceInvocationHandler(this), typeof(IInterfaceToFake), true);

            fakeObject.AMethod("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
        }

        [Test]
        public void TestSubInterface()
        {
            IInterfaceWithSubinterface fakeObject = (IInterfaceWithSubinterface)new ProxyFactory().CreateInterfaceProxy(new MyInterfaceInvocationHandler(this), typeof(IInterfaceWithSubinterface), true);


            fakeObject.TheSubInterface.AMethod("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
        }

        [Test]
        public void TestCallerDelegate()
        {
            m_MyInstanceMethodCalled = false;

            object[] parameters = new object[] { "world", 5, false };

            Action<object, object[]> caller = new ProxyFactory().CreateMethodCaller(s_MyInstanceMethodInfo).CallMethod;

            caller(this, parameters);

            Assert.IsTrue(m_MyInstanceMethodCalled);
        }

        [Test]
        public void TestReturningCallerDelegate()
        {
            m_MyInstanceMethodCalled = false;

            object[] parameters = new object[] { "world", 5, false };

            Func<object, object[], object> caller = new ProxyFactory().CreateMethodCaller(s_MyReturningInstanceMethodInfo).CallReturningMethod;

            object returned = caller(this, parameters);

            Assert.AreEqual("boo", returned);

            Assert.IsTrue(m_MyInstanceMethodCalled);
        }

        [Test]
        public void TestDelegateCallerDelegate()
        {
            m_MyInstanceMethodCalled = false;

            object[] parameters = new object[] { "world", 5, false };

            Action<object, object[]> caller = new ProxyFactory().CreateDelegateCaller(s_MyDelegateType, s_MyInstanceMethodInfo).CallMethod;

            caller(new MyDelegate(MyInstanceMethod), parameters);

            Assert.IsTrue(m_MyInstanceMethodCalled);
        }

        [Test]
        public void TestReturningDelegateCallerDelegate()
        {
            m_MyInstanceMethodCalled = false;

            object[] parameters = new object[] { "world", 5, false };

            Func<object, object[], object> caller = new ProxyFactory().CreateDelegateCaller(s_MyReturningDelegateType, s_MyReturningInstanceMethodInfo).CallReturningMethod;

            object returned = caller(new MyReturningDelegate(MyReturningInstanceMethod), parameters);

            Assert.AreEqual("boo", returned);

            Assert.IsTrue(m_MyInstanceMethodCalled);
        }

        class MyInterfaceInvocationHandler : IInterfaceInvocationHandler
        {
            private readonly ProxyFactoryTest m_Parent;

            public MyInterfaceInvocationHandler(ProxyFactoryTest parent)
            {
                m_Parent = parent;
            }

            public IMethodInvocationHandler GetInvocationHandlerFor(MethodCaller methodCaller, Type returnType)
            {
                return new MyInvocationHandler(m_Parent);
            }

        }

        private class MyInvocationHandler : IMethodInvocationHandler
        {
            private readonly ProxyFactoryTest m_Parent;

            public MyInvocationHandler(ProxyFactoryTest parent)
            {
                m_Parent = parent;
            }

            public void InvokeHappened(object[] parameterValues)
            {
                Assert.AreEqual("hello", parameterValues[0]);
                Assert.AreEqual(3, parameterValues[1]);
                Assert.AreEqual(true, parameterValues[2]);

                m_Parent.m_InvokeHappenedCalled = true;
            }

            public object ReturningInvokeHappened(object[] parameterValues)
            {
                // Bit of a hack, making a proxy for IInterfaceToFake
                return new ProxyFactory().CreateInterfaceProxy(new MyInterfaceInvocationHandler(m_Parent), typeof(IInterfaceToFake), true);
            }
        }

        public interface IInterfaceToFake : IActorComponent
        {
            void AMethod(string name, int num, bool flag);
        }

        public interface IInterfaceWithSubinterface
        {
            IInterfaceToFake TheSubInterface { get; } 
        }

        public delegate void MyDelegate(string s, int x, bool flag);
        public static void MyStaticMethod(string s, int x, bool flag)
        {
            Assert.AreEqual("world", s);
            Assert.AreEqual(5, x);
            Assert.AreEqual(false, flag);

            s_MyStaticMethodCalled = true;
        }

        public void MyInstanceMethod(string s, int x, bool flag)
        {
            Assert.AreEqual("world", s);
            Assert.AreEqual(5, x);
            Assert.AreEqual(false, flag);

            m_MyInstanceMethodCalled = true;
        }

        public delegate string MyReturningDelegate(string s, int x, bool flag);
        public string MyReturningInstanceMethod(string s, int x, bool flag)
        {
            Assert.AreEqual("world", s);
            Assert.AreEqual(5, x);
            Assert.AreEqual(false, flag);

            m_MyInstanceMethodCalled = true;

            return "boo";
        }

        public event MyDelegate EventToSignUpTo;

        public void InvokeEventToSignUpTo(string s, int x, bool flag)
        {
            MyDelegate handler = EventToSignUpTo;
            if (handler != null) handler(s, x, flag);
        }
    }
}
