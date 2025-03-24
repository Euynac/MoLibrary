using System;

namespace MoLibrary.Tool.Extensions;

public static class ActionExtensions
{
    public static void WrapAction<T>(ref Action<T>? innerAction, Action<T> outerAction)
    {
        if (innerAction != null)
        {
            var oldAction = innerAction;
            innerAction = data =>
            {
                outerAction.Invoke(data);
                oldAction(data);
            };
        }
        else
        {

            innerAction = outerAction;
        }
    }
}