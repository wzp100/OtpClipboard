using System.Text.Json;

namespace OtpClipboard.Api;

internal static class PipeCodec
{
    private const int MaxMessageBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken = default)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (payload.Length > MaxMessageBytes) throw new InvalidDataException("管道消息过大。");
        byte[] header = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken);
        int length = BitConverter.ToInt32(header);
        if (length is <= 0 or > MaxMessageBytes) throw new InvalidDataException("管道消息长度无效。");
        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidDataException("管道消息内容为空。");
    }
}
