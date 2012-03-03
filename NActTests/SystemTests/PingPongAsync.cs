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
            Thread.Sleep(1000);

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
                    s_Count = await m_Ponger.Pong(s_Count);
                }
            }
        }

        public interface IPinger : IActor
        {
            void Ping();
        }

        class Ponger : IPonger
        {
            public async Task<long> Pong(long count)
            {
                return count + 1;
            }

        }

        public interface IPonger : IActor
        {
            Task<long> Pong(long count);
        }
    }
}
