using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NAct;
using NUnit.Framework;

namespace NActTests.UnitTests
{
    [TestFixture]
    class InterfaceProxyFactoryTest
    {
        private bool m_InvokeHappened = false;

        [Test]
        public void BasicTest()
        {
            IInterfaceToFake fakeObject = (IInterfaceToFake) new InterfaceProxyFactory().CreateInterfaceProxy(new MyInvocationHandler(this), typeof(IInterfaceToFake));

            fakeObject.AMethod(true, "hello", 3);

            Assert.IsTrue(m_InvokeHappened);
        }

        class MyInvocationHandler : IInterfaceInvocationHandler
        {
            private readonly InterfaceProxyFactoryTest m_Parent;

            public MyInvocationHandler(InterfaceProxyFactoryTest parent)
            {
                m_Parent = parent;
            }

            public void InvokeHappened(MethodInfo method, object[] parameterValues)
            {
                Assert.AreEqual(true, parameterValues[0]);
                Assert.AreEqual("hello", parameterValues[1]);
                Assert.AreEqual(3, parameterValues[2]);

                m_Parent.m_InvokeHappened = true;
            }
        }

        interface IInterfaceToFake
        {
            void AMethod(bool flag, string name, int num);
        }
    }
}
