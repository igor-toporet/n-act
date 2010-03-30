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

        private static readonly MethodInfo s_MyStaticMethodInfo = typeof (ProxyFactoryTest).GetMethod("MyStaticMethod");

        [Test]
        public void TestDelegate()
        {
            m_InvokeHappenedCalled = false;

            // Make the proxy method
            MyStaticMethodDelegate proxyDelegate =
                (MyStaticMethodDelegate) new ProxyFactory().CreateMethodProxy(new MyStaticMethodInvocationHandler(this), s_MyStaticMethodInfo, typeof(MyStaticMethodDelegate));

            EventForMyStaticMethodToSignUpTo += proxyDelegate;

            InvokeEventForMyStaticMethodToSignUpTo("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
        }

        [Test]
        public void TestInterface()
        {
            IInterfaceToFake fakeObject = (IInterfaceToFake)new ProxyFactory().CreateInterfaceProxy(new MyInterfaceInvocationHandler(this), typeof(IInterfaceToFake));

            fakeObject.AMethod("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
        }

        class MyInterfaceInvocationHandler : IInterfaceInvocationHandler
        {
            private readonly ProxyFactoryTest m_Parent;

            public MyInterfaceInvocationHandler(ProxyFactoryTest parent)
            {
                m_Parent = parent;
            }

            public IMethodInvocationHandler GetInvocationHandlerFor(MethodInfo method)
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
        }

        public interface IInterfaceToFake
        {
            void AMethod(string name, int num, bool flag);
        }

        public delegate void MyStaticMethodDelegate(string s, int x, bool flag);
        public static void MyStaticMethod(string s, int x, bool flag)
        {
            throw new NotImplementedException();
        }

        public event MyStaticMethodDelegate EventForMyStaticMethodToSignUpTo;

        public void InvokeEventForMyStaticMethodToSignUpTo(string s, int x, bool flag)
        {
            MyStaticMethodDelegate handler = EventForMyStaticMethodToSignUpTo;
            if (handler != null) handler(s, x, flag);
        }
    }
}
