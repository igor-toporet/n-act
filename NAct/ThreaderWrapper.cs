using Castle.DynamicProxy;

namespace NAct
{
    public static class ThreaderWrapper
    {
        /// <summary>
        /// Creates a logical thread for a new actor, and then uses creator to instantiate the root object to form that actor.
        /// 
        /// This call does not wait for the creator to run, this is done asynchronously. However, any other methods called
        /// on the actor will not be processed until after the creator has run.
        /// </summary>
        /// <typeparam name="TInterface">The interface of the actor to create.</typeparam>
        /// <param name="creator">A function that creates a fresh instance of an object implementing TInterface.</param>
        /// <returns>An actor of type TInterface. The creator may not have run yet, but method calls on it will safely be queued for once it is created.</returns>
        public static TInterface CreateActor<TInterface>(ObjectCreator<TInterface> creator) where TInterface : class, IActor
        {
            CreatorInterceptor interceptor = new CreatorInterceptor(creator);
            return new ProxyGenerator().CreateInterfaceProxyWithoutTarget<TInterface>(interceptor);
        }
    }

    public delegate T ObjectCreator<out T>();
}
