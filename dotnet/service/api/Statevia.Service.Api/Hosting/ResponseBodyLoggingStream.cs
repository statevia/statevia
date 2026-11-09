namespace Statevia.Service.Api.Hosting;

/// <summary>
/// レスポンス本文の先頭 N バイトを保持しつつ下流ストリームへ転送する。
/// </summary>
internal sealed class ResponseBodyLoggingStream : Stream
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2213:DisposableFieldsShouldBeDisposed",
        Justification = "_inner のライフタイムは HttpContext.Response.Body の所有者が管理する。ラッパーは破棄しない。")]
    private readonly Stream _inner;
    private readonly int _maxCaptureBytes;
    private readonly MemoryStream _capture = new();
    private long _totalBytesWritten;

    /// <param name="inner">実レスポンスの書き込み先（Kestrel 等の下流）。</param>
    /// <param name="maxCaptureBytes">ログ用に保持する先頭バイト上限。</param>
    /// <summary>新しいインスタンスを初期化する。</summary>
    public ResponseBodyLoggingStream(Stream inner, int maxCaptureBytes)
    {
        _inner = inner;
        _maxCaptureBytes = maxCaptureBytes;
    }

    /// <summary>書き込んだ総バイト数（保持した分を超えても数える）。</summary>
    public long TotalBytesWritten => _totalBytesWritten;

    /// <summary>ログ用に保持した先頭のバイト列を返す。</summary>
    /// <returns>保持したバイト列のコピー。</returns>
    public ReadOnlyMemory<byte> GetCapturedBytes()
    {
        return _capture.ToArray();
    }

    /// <inheritdoc />
    public override bool CanRead => false;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override bool CanWrite => true;
    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc />
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        Capture(buffer.AsSpan(offset, count));
        _inner.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Capture(buffer);
        _inner.Write(buffer);
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Capture(buffer.AsSpan(offset, count));
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Capture(buffer.Span);
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    private void Capture(ReadOnlySpan<byte> buffer)
    {
        _totalBytesWritten += buffer.Length;
        // 上限到達後は転送のみ（メモリとログサイズを抑える）
        if (_capture.Length >= _maxCaptureBytes)
            return;
        var room = _maxCaptureBytes - (int)_capture.Length;
        var take = Math.Min(room, buffer.Length);
        _capture.Write(buffer[..take]);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _capture.Dispose();
        base.Dispose(disposing);
    }
}
