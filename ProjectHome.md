NAct is an Actors framework for C# and the .NET platform. Get it from [NuGet](http://nuget.org/packages/NAct).

## What are actors? ##

  * Objects that run in their own thread
  * Requests between actors are done asynchronously

## Why would I want to use Actors? ##

  * It's a really easy way of making a program run in parallel (so it's faster and more responsive)
  * There's no risk of thread-unsafety or deadlocks or race conditions

## Are actors a new idea? ##

No, actors are already popular in Scala, Erlang and F#. However, all of these require you to pass messages explicitly, throwing away the advantages of interfaces.

## What's different about NAct? ##

  * Message passing is simply calling a method on another actor: make sure the method either returns void or Task, and NAct's system runs the call in the actor's thread
  * C# 5 async/await is supported: if you call an async method that returns Task, then await on the result, and NAct will resume your method in the calling actor's thread
  * .NET events are supported, so if one actor signs up to an event on another actor, the event handler is called in the right thread
  * Supports Windows Forms and WPF easily - if your actor is also a Windows Forms or WPF control, NAct will automatically call it in the UI thread
  * It's a library for .NET so you can use it from C#, VB or any other .NET language

See [NAct in action](PingPong.md)

[Learn how to use NAct](Tutorial.md)

If you have any questions about how to use NAct, post a question on [StackOverflow](http://stackoverflow.com) tagged with "NAct".