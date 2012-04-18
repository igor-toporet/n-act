using System;
using System.Threading;

namespace NAct.Utils
{
    class Timer : ITimer
    {
        private bool m_AutoReset;

        private int m_Interval;

        public bool AutoReset
        {
            set { m_AutoReset = value; }
        }

        public int Interval
        {
            set { m_Interval = value; }
        }

        public event Action Elapsed;

        public void InvokeElapsed()
        {
            Action handler = Elapsed;
            if (handler != null) handler();
        }

        public void Start()
        {
            while (m_AutoReset)
            {
                Thread.Sleep(m_Interval);

                InvokeElapsed();
            }
        }
    }
}
