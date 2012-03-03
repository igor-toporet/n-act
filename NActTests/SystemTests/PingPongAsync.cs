using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAct;

namespace NActTests.SystemTests
{
    public class PingPongAsync
    {
        private static long s_Count;

        static void Main(string[] args)
        {
            IPonger ponger = ActorWrapper.WrapActor<IPonger>(() => new Ponger());
            IPinger pinger = ActorWrapper.WrapActor<IPinger>(() => new Pinger(ponger));

            pinger.Ping();
            Thread.Sleep(10000);

            Console.WriteLine(s_Count);
            Console.ReadKey();
        }

        class Pinger : IPinger
        {
            private readonly IPonger m_Ponger;

            public Pinger(IPonger ponger)
            {
                m_Ponger = ponger;
            }

            public async void Ping()
            {
                while (true)
                {
                    Console.WriteLine("Ping!");
                    Monitor.Pulse(this); // Verify our thread owns the Actor's lock
                    await m_Ponger.Pong();
                }
            }
        }

        public interface IPinger : IActor
        {
            void Ping();
        }

        class Ponger : IPonger
        {
            public async Task Pong()
            {
                Console.WriteLine("Pong!");
                Monitor.Pulse(this); // Verify our thread owns the Actor's lock
                s_Count++;
            }

        }

        public interface IPonger : IActor
        {
            Task Pong();
        }
    }
}
