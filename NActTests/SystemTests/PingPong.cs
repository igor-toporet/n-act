using System;
using System.Threading;
using NAct;
using NAct.Utils;
using NUnit.Framework;

namespace NActTests.SystemTests
{
    [TestFixture]
    public class PingPong
    {
        private static int s_Count = 0;

        [Test]
        public static void Main(string[] args)
        {
            IPonger ponger = ActorWrapper.WrapActor<IPonger>(() => new Ponger());
            IPinger pinger = ActorWrapper.WrapActor<IPinger>(() => new Pinger(ponger));
            //IPonger ponger = new Ponger();
            //IPinger pinger = new Pinger(ponger);

            new Thread(delegate(object o)
                           {
                               Thread.Sleep(10000);

                               Console.WriteLine(s_Count);
                               Thread.Sleep(1000);
                           }).Start();

            pinger.Ping();
        }

        public interface IPinger : IActor
        {
            void Ping();
        }

        public interface IPonger : IActor
        {
            void Pong(Action callback);

            event Action Ponged;
        }

        class Pinger : IPinger
        {
            private readonly IPonger m_Ponger;

            public Pinger(IPonger ponger)
            {
                m_Ponger = ponger;

                //m_Ponger.Ponged += Ping;
            }

            public void Ping()
            {
                m_Ponger.Pong(Ping);
            }
        }

        class Ponger : IPonger
        {
            public void Pong(Action callback)
            {
                s_Count++;
                callback();
                //Ponged();
            }

            public event Action Ponged;
        }
    }
}


