using System;
using System.Reflection;
using NAct.Utils;

namespace NAct
{
    public class Hooking
    {
        public static Action<Action> ActorCallWrapper { internal get; set; }
        public static Action<Type, MethodInfo> BeforeActorMethodRun { internal get; set; }
        public static Action<Type, MethodInfo, object[]> BeforeActorCallQueued { internal get; set; }


        static Hooking()
        {
            ActorCallWrapper = DefaultActorCallWrapper;
            BeforeActorMethodRun = DoNothing;
            BeforeActorCallQueued = DoNothing;
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

        private static void DoNothing(Type type, MethodInfo methodInfo)
        {

        }

        private static void DoNothing(Type type, MethodInfo methodInfo, object[] parameters)
        {

        }
    }
}
