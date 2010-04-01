using System;
using System.Threading;
using NAct;
using NUnit.Framework;

namespace NActTests.SystemTests
{
    [TestFixture]
    public class PingPong
    {
        private static int s_Count = 0;

        [Test]
        public void Run()
        {
            IPonger ponger = ActorWrapper.WrapActor<IPonger>(() => new Ponger());
            IPinger pinger = ActorWrapper.WrapActor<IPinger>(() => new Pinger(ponger));

            pinger.Ping();
            Thread.Sleep(1000);

            Console.WriteLine(s_Count);
        }

        public interface IPinger : IActor
        {
            void Ping();
        }

        public interface IPonger : IActor
        {
            void Pong();

            event Action Ponged;
        }

        class Pinger : IPinger
        {
            private readonly IPonger m_Ponger;

            public Pinger(IPonger ponger)
            {
                m_Ponger = ponger;

                m_Ponger.Ponged += Ping;
            }

            public void Ping()
            {
                m_Ponger.Pong();
            }
        }

        class Ponger : IPonger
        {
            public void Pong()
            {
                s_Count++;
                Ponged();
            }

            public event Action Ponged;
        }
    }
}


