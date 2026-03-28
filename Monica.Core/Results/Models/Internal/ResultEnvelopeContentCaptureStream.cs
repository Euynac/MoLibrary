using System.Net.Http.Headers;

namespace Monica.Core.Results.Internal;

internal sealed class ResultEnvelopeContentCaptureStream(Stream innerStream, int maxCaptureBytes) : Stream
{
    private readonly Stream _innerStream = innerStream;
    private readonly MemoryStream _capturedBytes = new(Math.Max(0, maxCaptureBytes));
    private readonly int _maxCaptureBytes = Math.Max(0, maxCaptureBytes);
    private bool _isDisposed;
    private bool _isTruncated;

    public override bool CanRead => !_isDisposed && _innerStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public ResultEnvelopeCapturedContent ToCapturedContent(HttpContentHeaders? headers)
    {
        return new ResultEnvelopeCapturedContent(
            ResultEnvelopeCapturedContent.DecodeBytes(_capturedBytes.ToArray(), headers),
            _isTruncated);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _innerStream.Read(buffer, offset, count);
        Capture(buffer.AsSpan(offset, bytesRead));
        return bytesRead;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesRead = _innerStream.Read(buffer);
        Capture(buffer[..bytesRead]);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await _innerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        Capture(buffer.AsSpan(offset, bytesRead));
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await _innerStream.ReadAsync(buffer, cancellationToken);
        Capture(buffer.Span[..bytesRead]);
        return bytesRead;
    }

    public override void Flush()
    {
        _innerStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _innerStream.FlushAsync(cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        throw new NotSupportedException();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            _capturedBytes.Dispose();
            _innerStream.Dispose();
        }

        _isDisposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            await base.DisposeAsync();
            return;
        }

        await _capturedBytes.DisposeAsync();
        await _innerStream.DisposeAsync();
        _isDisposed = true;
        await base.DisposeAsync();
    }

    private void Capture(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        var remaining = _maxCaptureBytes - (int)_capturedBytes.Length;
        if (remaining <= 0)
        {
            _isTruncated = true;
            return;
        }

        var bytesToCapture = Math.Min(buffer.Length, remaining);
        _capturedBytes.Write(buffer[..bytesToCapture]);

        if (bytesToCapture < buffer.Length)
        {
            _isTruncated = true;
        }
    }
}
