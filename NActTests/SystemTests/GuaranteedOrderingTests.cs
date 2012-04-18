using System.Threading;
using NAct;
using NUnit.Framework;

namespace NActTests.SystemTests
{
    [TestFixture]
    public class GuaranteedOrderingTests
    {
        private const int c_Trials = 100;

        [Test]
        public void MethodsRunInOrder()
        {
            ActingCompleteWaiter.Reset();

            var setAndCheckActor = ActorWrapper.WrapActor<ISetAndCheckActor>(() => new SetAndCheckActor());

            for(int i = 0; i < c_Trials; i++)
            {
                setAndCheckActor.SetValue(i);
                setAndCheckActor.CheckValue(i);
            }

            ActingCompleteWaiter.WaitForActionsToComplete(1000);
        }
    }

    public interface ISetAndCheckActor : IActor
    {
        void SetValue(int value);
        void CheckValue(int value);
    }

    public class SetAndCheckActor : ISetAndCheckActor
    {
        private int m_Value;

        public void SetValue(int value)
        {
            m_Value = value;
        }

        public void CheckValue(int value)
        {
            Assert.AreEqual(value, m_Value);
        }
    }
}