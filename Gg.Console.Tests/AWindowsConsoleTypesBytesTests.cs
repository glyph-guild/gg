using System.Text;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// What a Windows console read gives, handed on as the bytes a terminal would have sent.
/// </summary>
/// <remarks>
/// <b>The host forwards bytes</b> (see <see cref="IHostTerminal.Keystrokes"/>), and a
/// Windows console read gives UTF-16. Converted here: a character outside the basic plane
/// can arrive as two reads, and a caller's buffer can be smaller than what is waiting.
/// </remarks>
public class AWindowsConsoleTypesBytesTests
{
    private static byte[] Drain(KeystrokeBuffer buffer, int chunk = 64)
    {
        var all = new List<byte>();
        var bytes = new byte[chunk];
        int read;
        while ((read = buffer.Read(bytes, 0, bytes.Length)) > 0)
        {
            all.AddRange(bytes.AsSpan(0, read).ToArray());
        }

        return [.. all];
    }

    [Test]
    public async Task An_empty_buffer_reads_nought()
    {
        await Assert.That(new KeystrokeBuffer().Read(new byte[8], 0, 8)).IsEqualTo(0);
    }

    [Test]
    public async Task Text_comes_out_as_utf8()
    {
        var buffer = new KeystrokeBuffer();
        buffer.Append("é\u001b[A");

        await Assert.That(Drain(buffer)).IsEquivalentTo(Encoding.UTF8.GetBytes("é\u001b[A"));
    }

    [Test]
    public async Task A_character_split_across_two_reads_is_joined()
    {
        const string emoji = "\U0001F600";
        var buffer = new KeystrokeBuffer();

        buffer.Append(emoji.AsSpan(0, 1));
        await Assert.That(Drain(buffer)).IsEmpty()
            .Because("half a character has no bytes of its own; sent alone it would become a replacement character.");

        buffer.Append(emoji.AsSpan(1, 1));
        await Assert.That(Drain(buffer)).IsEquivalentTo(Encoding.UTF8.GetBytes(emoji));
    }

    [Test]
    public async Task A_small_read_leaves_the_rest_for_the_next()
    {
        var buffer = new KeystrokeBuffer();
        buffer.Append("abcdef");

        var first = new byte[4];
        await Assert.That(buffer.Read(first, 0, 4)).IsEqualTo(4);
        await Assert.That(Drain(buffer)).IsEquivalentTo("ef"u8.ToArray());
    }
}
