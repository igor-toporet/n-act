using System;
using System.Reflection;
using NAct;
using NUnit.Framework;

namespace NActTests.UnitTests
{
    [TestFixture]
    class MethodProxyFactoryTest
    {
        private bool m_InvokeHappenedCalled = false;

        private static readonly MethodInfo s_MyStaticMethodInfo = typeof (MethodProxyFactoryTest).GetMethod("MyStaticMethod");

        [Test]
        public void TestStaticMethod()
        {
            // Make the proxy method
            MyStaticMethodDelegate proxyDelegate =
                (MyStaticMethodDelegate) MethodProxyFactory.CreateMethodProxy<MyStaticMethodDelegate>(new MyStaticMethodInvocationHandler(this), s_MyStaticMethodInfo);

            EventForMyStaticMethodToSignUpTo += proxyDelegate;

            InvokeEventForMyStaticMethodToSignUpTo("hello", 3, true);

            Assert.IsTrue(m_InvokeHappenedCalled);
        }

        private class MyStaticMethodInvocationHandler : IInvocationHandler
        {
            private readonly MethodProxyFactoryTest m_Parent;

            public MyStaticMethodInvocationHandler(MethodProxyFactoryTest parent)
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
