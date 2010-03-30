using System;
using System.Threading;
using NAct;
using NUnit.Framework;

namespace NActTests.SystemTests
{
    [TestFixture]
    public class PingPong
    {
        [Test]
        public void Run()
        {
            IPonger ponger = ThreaderWrapper.CreateActor<IPonger>(() => new Ponger());
            IPinger pinger = ThreaderWrapper.CreateActor<IPinger>(() => new Pinger(ponger));

            pinger.Ping();
            Thread.Sleep(10000);

            ponger.Count(i => Console.WriteLine(i * 2));

            Thread.Sleep(100);
        }

        public interface IPinger : IActor
        {
            void Ping();
        }

        public interface IPonger : IActor
        {
            void Pong();
            
            event Action Ponged;

            void Count(Action<int> callback);
        }

        // Something that prints ping and calls Pong on a Ponger every once in a while
        private class Pinger : IPinger
        {
            private readonly IPonger m_Ponger;

            public Pinger(IPonger ponger)
            {
                m_Ponger = ponger;

                m_Ponger.Ponged += Ping;
            }

            public void Ping()
            {
                Console.WriteLine("Ping on thread: " + Thread.CurrentThread.ManagedThreadId);

                m_Ponger.Pong();
            }
        }

        private class Ponger : IPonger
        {
            private int m_Count = 0;

            public void Pong()
            {
                Console.WriteLine("Pong on thread: " + Thread.CurrentThread.ManagedThreadId);

                m_Count++;
                InvokePonged();
            }

            public void Count(Action<int> callback)
            {
                callback(m_Count);
            }

            public event Action Ponged;

            private void InvokePonged()
            {
                Action handler = Ponged;
                if (handler != null) handler();
            }
        }

        public delegate void Action();
    }


}


