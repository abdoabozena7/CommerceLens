namespace CommerceLens.Api.Services;

public static class UploadReader
{
    public static async Task<byte[]> ReadAsync(Stream source, int maxBytes, string limitMessage, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int size;
        while ((size = await source.ReadAsync(chunk, token)) != 0)
        {
            if (buffer.Length + size > maxBytes) throw new ApiException(413, limitMessage);
            await buffer.WriteAsync(chunk.AsMemory(0, size), token);
        }
        return buffer.ToArray();
    }
}
