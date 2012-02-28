using NAct;
using NActTests.SystemTests;
using NUnit.Framework;

namespace NActTests.Performance_Tests
{
    [TestFixture]
    public class CallingCycleTest
    {
        private const int m_Total = 50000;

        [Test, Explicit]
        public void TimeCountingUp()
        {
            ActingCompleteWaiter.Reset();

            var firstActor = ActorWrapper.WrapActor<ICallAroundActor>(() => new CallAroundActor());
            var secondActor = ActorWrapper.WrapActor<ICallAroundActor>(() => new CallAroundActor());
            var thirdActor = ActorWrapper.WrapActor<ICallAroundActor>(() => new CallAroundActor());

            firstActor.SetNextActor(secondActor);
            secondActor.SetNextActor(thirdActor);
            thirdActor.SetNextActor(firstActor);

            firstActor.CountUp(0, m_Total);

            ActingCompleteWaiter.WaitForActionsToComplete(1000);
        }
    }

    public interface ICallAroundActor : IActor
    {
        void SetNextActor(ICallAroundActor nextActor);
        void CountUp(int count, int total);
    }

    public class CallAroundActor : ICallAroundActor
    {
        private ICallAroundActor m_NextActor;

        public void SetNextActor(ICallAroundActor nextActor)
        {
            m_NextActor = nextActor;
        }

        public void CountUp(int count, int total)
        {
            count++;
            if(count <= total)
            {
                m_NextActor.CountUp(count, total);
            }
        }
    }
}
