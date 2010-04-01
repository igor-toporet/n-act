using System;
using System.Reflection;
using NAct;
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

        [Test]
        public void TestDelegate()
        {
            m_InvokeHappenedCalled = false;

            // Make the proxy method
            MyStaticMethodDelegate proxyDelegate =
                (MyStaticMethodDelegate) new ProxyFactory().CreateDelegateProxy(new MyStaticMethodInvocationHandler(this), s_MyStaticMethodInfo, typeof(MyStaticMethodDelegate));

            EventForMyStaticMethodToSignUpTo += proxyDelegate;

            InvokeEventForMyStaticMethodToSignUpTo("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
        }

        [Test]
        public void TestInterface()
        {
            IInterfaceToFake fakeObject = (IInterfaceToFake)new ProxyFactory().CreateInterfaceProxy(new MyInterfaceInvocationHandler(this), typeof(IInterfaceToFake), true);

            fakeObject.AMethod("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
        }

        [Test]
        public void TestCallerDelegate()
        {
            m_MyInstanceMethodCalled = false;

            object[] parameters = new object[] { "world", 5, false };

            Action<object, object[]> caller = new ProxyFactory().CreateCallerDelegate(s_MyInstanceMethodInfo);

            caller(this, parameters);

            Assert.IsTrue(m_MyInstanceMethodCalled);
        }

        [Test]
        public void TestReturningCallerDelegate()
        {
            m_MyInstanceMethodCalled = false;

            object[] parameters = new object[] { "world", 5, false };

            Func<object, object[], object> caller = new ProxyFactory().CreateReturningCallerDelegate(s_MyReturningInstanceMethodInfo);

            object returned = caller(this, parameters);

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

            public IMethodInvocationHandler GetInvocationHandlerFor(MethodCaller methodCaller)
            {
                return new MyStaticMethodInvocationHandler(m_Parent);
            }
        }

        private class MyStaticMethodInvocationHandler : IMethodInvocationHandler
        {
            private readonly ProxyFactoryTest m_Parent;

            public MyStaticMethodInvocationHandler(ProxyFactoryTest parent)
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
                throw new NotImplementedException();
            }
        }

        public interface IInterfaceToFake
        {
            void AMethod(string name, int num, bool flag);
        }

        public delegate void MyStaticMethodDelegate(string s, int x, bool flag);
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

        public string MyReturningInstanceMethod(string s, int x, bool flag)
        {
            Assert.AreEqual("world", s);
            Assert.AreEqual(5, x);
            Assert.AreEqual(false, flag);

            m_MyInstanceMethodCalled = true;

            return "boo";
        }

        public event MyStaticMethodDelegate EventForMyStaticMethodToSignUpTo;

        public void InvokeEventForMyStaticMethodToSignUpTo(string s, int x, bool flag)
        {
            MyStaticMethodDelegate handler = EventForMyStaticMethodToSignUpTo;
            if (handler != null) handler(s, x, flag);
        }
    }
}
