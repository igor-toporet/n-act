using System;
using NAct.Utils;

namespace NAct
{
    public class Hooking
    {
        internal static Action<Action> Hook { get; set; }

        static Hooking()
        {
            SetHook(DefaultHook);
        }

        private static void DefaultHook(Action action)
        {
            // By default, just call the stuff in a try-catch
            try
            {
                action();
            }
            catch
            {
            }
        }

        public static void SetHook(Action<Action> hook)
        {
            Hook = hook;
        }
    }
}
