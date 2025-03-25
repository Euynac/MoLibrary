using System;
using System.Threading;

namespace MoLibrary.Tool.Extensions;

/// <summary>
/// Extension methods to make locking easier.
/// </summary>
public static class LockExtensions
{
    /// <summary>
    /// Executes given <paramref name="action"/> by locking given <paramref name="source"/> object.
    /// </summary>
    /// <param name="source">Source object (to be locked)</param>
    /// <param name="action">Action (to be executed)</param>
    public static void Locking(this object source, Action action)
    {
        lock (source)
        {
            action();
        }
    }

    /// <summary>
    /// Executes given <paramref name="action"/> by locking given <paramref name="source"/> object.
    /// </summary>
    /// <typeparam name="T">Type of the object (to be locked)</typeparam>
    /// <param name="source">Source object (to be locked)</param>
    /// <param name="action">Action (to be executed)</param>
    public static void Locking<T>(this T source, Action<T> action) where T : class
    {
        lock (source)
        {
            action(source);
        }
    }

    /// <summary>
    /// Executes given <paramref name="func"/> and returns it's value by locking given <paramref name="source"/> object.
    /// </summary>
    /// <typeparam name="TResult">Return type</typeparam>
    /// <param name="source">Source object (to be locked)</param>
    /// <param name="func">Function (to be executed)</param>
    /// <returns>Return value of the <paramref name="func"/></returns>
    public static TResult Locking<TResult>(this object source, Func<TResult> func)
    {
        lock (source)
        {
            return func();
        }
    }

    /// <summary>
    /// Executes given <paramref name="func"/> and returns it's value by locking given <paramref name="source"/> object.
    /// </summary>
    /// <typeparam name="T">Type of the object (to be locked)</typeparam>
    /// <typeparam name="TResult">Return type</typeparam>
    /// <param name="source">Source object (to be locked)</param>
    /// <param name="func">Function (to be executed)</param>
    /// <returns>Return value of the <paramnref name="func"/></returns>
    public static TResult Locking<T, TResult>(this T source, Func<T, TResult> func) where T : class
    {
        lock (source)
        {
            return func(source);
        }
    }
    /// <summary>
    /// Doing read action in a read lock.
    /// </summary>
    /// <param name="rwLock"></param>
    /// <param name="action"></param>
    public static T DoReadAction<T>(this ReaderWriterLockSlim rwLock, Func<T> action)
    {
        rwLock.EnterReadLock();
        try
        {
            return action();
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }
    /// <summary>
    /// Doing read action in a read lock.
    /// </summary>
    /// <param name="rwLock"></param>
    /// <param name="action"></param>
    public static void DoReadAction(this ReaderWriterLockSlim rwLock, Action action)
    {
        rwLock.EnterReadLock();
        try
        {
            action();
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Doing write action in a write lock.
    /// </summary>
    /// <param name="rwLock"></param>
    /// <param name="action"></param>
    public static void DoWriteAction(this ReaderWriterLockSlim rwLock, Action action)
    {
        rwLock.EnterWriteLock();
        try
        {
            action();
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Doing write action in a write lock.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="rwLock"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T DoWriteAction<T>(this ReaderWriterLockSlim rwLock, Func<T> action)
    {
        rwLock.EnterWriteLock();
        try
        {
            return action();
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }
}
