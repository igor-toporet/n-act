using System;
using System.Threading;
using NAct;
using NUnit.Framework;

namespace NActTests.SystemTests
{
    public static class ActingCompleteWaiter
    {
        private static ManualResetEvent m_WaitEvent = new ManualResetEvent(false);
        private static int m_OutstandingJobs = 0;
        private static volatile Exception m_Exception = null;

        static ActingCompleteWaiter()
        {
            Hooking.BeforeActorCallQueued = delegate
                                                {
                                                    Interlocked.Increment(ref m_OutstandingJobs);
                                                    m_WaitEvent.Reset();
                                                };
            Hooking.ActorCallWrapper = action =>
                                           {
                                               try
                                               {
                                                   action();
                                               }
                                               catch(Exception e)
                                               {
                                                   m_Exception = e;
                                                   m_WaitEvent.Set();
                                               }
                                               finally
                                               {
                                                   int result = Interlocked.Decrement(ref m_OutstandingJobs);
                                                   if (result == 0)
                                                   {
                                                       m_WaitEvent.Set();
                                                   }
                                               }
                                           };
        }

        public static void Reset()
        {
            m_WaitEvent.Reset();
            Interlocked.Exchange(ref m_OutstandingJobs, 0);
            m_Exception = null;
        }

        public static void WaitForActionsToComplete(int milliseconds)
        {
            if ( ! m_WaitEvent.WaitOne(milliseconds) )
            {
                Assert.Fail("Actors timed out");
            }
            if(m_Exception != null)
            {
                throw m_Exception;
            }
        }
        
    }
}