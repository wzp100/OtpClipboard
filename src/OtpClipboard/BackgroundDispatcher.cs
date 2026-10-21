using System.Collections.Concurrent;

namespace OtpClipboard;

/// <summary>
/// 后台 STA 线程的任务队列。WinRT 回调、异步续体和窗口消息
/// 最终都回到同一个线程，避免跨线程创建或操作桌面窗口。
/// </summary>
internal sealed class BackgroundDispatcher : SynchronizationContext, IDisposable
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _operations = new();
    private readonly AutoResetEvent _wake = new(false);
    private int _disposed;

    public WaitHandle WaitHandle => _wake;

    public override void Post(SendOrPostCallback callback, object? state) => Enqueue(callback, state);

    public override void Send(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (SynchronizationContext.Current == this)
        {
            callback(state);
            return;
        }

        using var completed = new ManualResetEventSlim();
        Exception? error = null;
        Enqueue(_ =>
        {
            try { callback(state); }
            catch (Exception ex) { error = ex; }
            finally { completed.Set(); }
        }, null);
        completed.Wait();
        if (error is not null) throw new AggregateException(error);
    }

    public Task InvokeAsync(Func<Task> action, TimeSpan timeout)
        => InvokeAsync(action, timeout, CancellationToken.None);

    public Task InvokeAsync(Func<Task> action, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(async _ =>
        {
            try { await action(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }, null);
        return completion.Task.WaitAsync(timeout, cancellationToken);
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> action, TimeSpan timeout)
        => InvokeAsync(action, timeout, CancellationToken.None);

    public Task<T> InvokeAsync<T>(Func<Task<T>> action, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(async _ =>
        {
            try { completion.TrySetResult(await action()); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }, null);
        return completion.Task.WaitAsync(timeout, cancellationToken);
    }

    private void Enqueue(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (Volatile.Read(ref _disposed) != 0) return;
        _operations.Enqueue((callback, state));
        _wake.Set();
    }

    public void Drain() { while (_operations.TryDequeue(out var item)) item.Callback(item.State); }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _wake.Dispose();
    }
}
