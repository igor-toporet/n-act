namespace NAct
{
    public static class ActorWrapper
    {
        /// <summary>
        /// Yes, I know, this is a singleton. I'm sorry. But I really didn't want to burden my users with injecting this.
        /// They wouldn't understand why.
        /// </summary>
        private static readonly ProxyFactory s_GlobalProxyFactory = new ProxyFactory();

        /// <summary>
        /// Creates a logical thread for a new actor, and then uses creator to instantiate the root object to form that actor.
        /// 
        /// This call does not wait for the creator to run, this is done asynchronously. However, any other methods called
        /// on the actor will not be processed until after the creator has run.
        /// </summary>
        /// <typeparam name="TActorType">The interface of the actor to create.</typeparam>
        /// <param name="creator">A function that creates a fresh instance of an object implementing TInterface.</param>
        /// <returns>An actor of type TInterface. The creator may not have run yet, but method calls on it will safely be queued for once it is created.</returns>
        public static TActorType WrapActor<TActorType>(ObjectCreator<IActor> creator) where TActorType : class, IActor
        {
            CreatorInterfaceInvocationHandler creatorInvocationHandler = new CreatorInterfaceInvocationHandler(creator, s_GlobalProxyFactory);
            return (TActorType)s_GlobalProxyFactory.CreateInterfaceProxy(creatorInvocationHandler, typeof(TActorType), true);
        }

        /// <summary>
        /// Wraps up an existing raw actor to make it callable safely. Don't call the raw actor before doing this!!
        /// </summary>
        /// <typeparam name="TActorType">The interface of the actor to create.</typeparam>
        /// <param name="actor">The raw actor to wrap.</param>
        /// <returns>An actor of type TInterface.</returns>
        public static TActorType WrapActor<TActorType>(TActorType actor) where TActorType : class, IActor
        {
            ActorInterfaceInvocationHandler actorInterfaceInvocationHandler = new ActorInterfaceInvocationHandler(actor, actor, s_GlobalProxyFactory);
            return (TActorType)s_GlobalProxyFactory.CreateInterfaceProxy(actorInterfaceInvocationHandler, typeof(TActorType), true);
        }

        /// <summary>
        /// Wraps an object (which isn't an actor) so that it's safe to be talked to by an actor.
        /// 
        /// In particular, this allows an actor to sign up to events on this object, and be correctly called back in its own thread.
        /// 
        /// Remember that when an actor calls a non-actor, it can use any thread, and the actor is blocked while doing so.
        /// 
        /// This call waits for the creator to run.
        /// </summary>
        /// <typeparam name="TAudienceType">The interface of the audience object to create.</typeparam>
        /// <param name="audienceObject">The object to make safe for actors to call.</param>
        /// <returns>An audience object which is safe for actors to sign up to.</returns>
        public static TAudienceType WrapAudience<TAudienceType>(TAudienceType audienceObject) where TAudienceType : class
        {
            AudienceInterfaceInvocationHandler invocationHandler = new AudienceInterfaceInvocationHandler(audienceObject, s_GlobalProxyFactory);
            return (TAudienceType) s_GlobalProxyFactory.CreateInterfaceProxy(invocationHandler, typeof (TAudienceType), false);
        }
    }
}
