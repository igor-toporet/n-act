namespace NAct.Utils
{
    interface ITimer : IActor
    {
        bool AutoReset { set; }

        int Interval { set; }

        event Action Elapsed;

        void Start();
    }
}
