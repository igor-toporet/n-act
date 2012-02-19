using System;
using System.Threading;
using NAct;
using NAct.Utils;
using NUnit.Framework;

namespace NActTests.SystemTests
{
    [TestFixture]
    public class DelegateWrappingTests
    {
        [Test]
        public void TestDelegateUsesRightLock()
        {
            var directActor = ActorWrapper.WrapActor<IDirectActor>(() => new DirectActor());
            var indirectActor = ActorWrapper.WrapActor<IIndirectActor>(() => new IndirectActor());
            var runningActor = ActorWrapper.WrapActor<IRunningActor>(() => new RunningActor());

            directActor.InitialMethod(runningActor, indirectActor);
            Thread.Sleep(300);
        }
    }

    public interface IDirectActor : IActor
    {
        void InitialMethod(IRunningActor runner, IIndirectActor toCall);
        void LongRunningMethod();
    }


    class DirectActor : IDirectActor
    {
        private int m_CurrentlyRunning = 0;

        public void InitialMethod(IRunningActor runner, IIndirectActor toCall)
        {
            runner.RunIt(LongRunningMethod);
            toCall.RunItIndirectly(runner, this);
        }

        public void LongRunningMethod()
        {
            int afterIncrement = Interlocked.Increment(ref m_CurrentlyRunning);
            Assert.AreEqual(1, afterIncrement, "More than one instance running at once");
            Thread.Sleep(100);
            Interlocked.Decrement(ref m_CurrentlyRunning);
        }
    }

    public interface IIndirectActor : IActor
    {
        void RunItIndirectly(IRunningActor runner, IDirectActor caller);
    }

    class IndirectActor : IIndirectActor
    {
        public void RunItIndirectly(IRunningActor runner, IDirectActor caller)
        {
            runner.RunIt(caller.LongRunningMethod);
        }
    }

    public interface IRunningActor : IActor
    {
        void RunIt(Action action);
    }

    class RunningActor : IRunningActor
    {
        public void RunIt(Action action)
        {
            action();
        }
    }
}