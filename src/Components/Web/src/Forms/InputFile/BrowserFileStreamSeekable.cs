// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Forms;

internal sealed class BrowserFileStreamSeekable : Stream
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ElementReference _inputFileElement;
    private readonly BrowserFile _file;
    private readonly long _maxAllowedSize;
    private readonly CancellationTokenSource _openReadStreamCts;
    private readonly CancellationToken _cancellationToken;

    private long _position;
    private bool _isDisposed;

    public BrowserFileStreamSeekable(
        IJSRuntime jsRuntime,
        ElementReference inputFileElement,
        BrowserFile file,
        long maxAllowedSize,
        CancellationToken cancellationToken)
    {
        _jsRuntime = jsRuntime;
        _inputFileElement = inputFileElement;
        _file = file;
        _maxAllowedSize = maxAllowedSize;
        _cancellationToken = cancellationToken;
        _openReadStreamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _file.Size;

    public override long Position
    {
        get
        {
            EnsureNotDisposed();
            return _position;
        }
        set
        {
            EnsureNotDisposed();
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Position must be non-negative.");
            }
            _position = value;
        }
    }

    public override void Flush()
        => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Synchronous reads are not supported.");

    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotDisposed();

        long newPosition;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPosition = offset;
                break;
            case SeekOrigin.Current:
                newPosition = _position + offset;
                break;
            case SeekOrigin.End:
                newPosition = Length + offset;
                break;
            default:
                throw new ArgumentException("Invalid seek origin.", nameof(origin));
        }

        if (newPosition < 0)
        {
            throw new IOException("Cannot seek to a position before the beginning of the stream.");
        }

        if (newPosition > Length)
        {
            throw new IOException("Cannot seek beyond the end of the stream.");
        }

        _position = newPosition;
        return _position;
    }

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        var bytesAvailableToRead = Length - _position;
        if (bytesAvailableToRead <= 0)
        {
            return 0;
        }

        var maxBytesToRead = (int)Math.Min(bytesAvailableToRead, buffer.Length);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _openReadStreamCts.Token, cancellationToken);

        await using var blob = await _jsRuntime.InvokeAsync<IJSStreamReference>(
            InputFileInterop.ReadFileDataAtPosition,
            linkedCts.Token,
            _inputFileElement,
            _file.Id,
            _position,
            maxBytesToRead);

        await using var blobStream = await blob.OpenReadStreamAsync(_maxAllowedSize, linkedCts.Token);
        var bytesRead = await blobStream.ReadAsync(buffer.Slice(0, maxBytesToRead), linkedCts.Token);

        _position += bytesRead;

        return bytesRead;
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(BrowserFileStreamSeekable));
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _openReadStreamCts.Cancel();
        _openReadStreamCts.Dispose();

        _isDisposed = true;

        base.Dispose(disposing);
    }
}