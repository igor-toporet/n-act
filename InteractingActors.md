## Why can't methods on actors return a value? ##

Actors need a style of programming called asynchronous programming. That means that when one actor calls a method on another, it doesn't wait for that method to finish. In fact, it doesn't even wait for it to start. The method could get called at any point in the future.

So when you need to get some information out of another actor, you have to think more like people communicating by post. You send a message that requests the information, then the other actor sends you a message with the result.

Using C# async methods, you can make these two messages look like a simple method call. When you call a method on another actor, you are returned a Task`<T>`. This is like a promise of a T at some point in the future. You can then use await to pause your method, and resume when the reply message arrives.

## What does an asynchronous interface look like? ##

Here's a typical asynchronous method, that lets you ask an actor for an integer:

```
Task<int> GetTheNumber();
```

So your message to the other actor just contains the function that it should call on you to give you the result. Simple, no?

## What does it look like to call an asynchronous method? ##

The easiest way to use a result from a call to another actor is using await:

```
    int n = await otherActor.GetTheNumber();

    // Do something with n
    ...
```