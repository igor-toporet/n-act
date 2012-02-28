using System;
using NAct.Utils;

namespace NAct
{
    public class Hooking
    {
        public static Action<Action> ActorCallWrapper { internal get; set; }
        public static Action BeforeQueueActorCall { internal get; set; }


        static Hooking()
        {
            ActorCallWrapper = DefaultActorCallWrapper;
            BeforeQueueActorCall = DoNothing;
        }

        private static void DefaultActorCallWrapper(Action action)
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

        private static void DoNothing()
        {
            
        }
    }
}
