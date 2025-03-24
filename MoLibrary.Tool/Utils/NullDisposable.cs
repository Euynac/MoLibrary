using System;
using System.Threading.Tasks;

namespace MoLibrary.Tool.Utils;

public sealed class NullDisposable : IDisposable
{
    public static NullDisposable Instance { get; } = new();

    private NullDisposable()
    {

    }

    public void Dispose()
    {
    }
}

public sealed class NullAsyncDisposable : IAsyncDisposable
{
    public static NullAsyncDisposable Instance { get; } = new();

    private NullAsyncDisposable()
    {

    }

    public ValueTask DisposeAsync()
    {
        return default;
    }
}