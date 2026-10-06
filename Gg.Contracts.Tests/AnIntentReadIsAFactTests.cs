using System.Text.Json;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// <b>S62.1-02</b> - <c>intent.read</c> is a fact kind carrying repository, path, requested ref,
/// commit, blob id and size.
/// </summary>
/// <remarks>
/// <b>The record of what the agent was given, and the pin everything carried from it uses.</b>
/// The control plane reads no repository bytes, so it cannot know which commit a ref named when
/// the runner read it. This fact is the only place that answer is made, and a leg opened from
/// the flight reads its commit rather than the ref.
/// </remarks>
public class AnIntentReadIsAFactTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string Blob = "89abcdef0123456789abcdef0123456789abcdef";

    private static IntentRead Read() => new()
    {
        Repository = "JDX/JDNext",
        Path = "docs/plans/18291.md",
        RequestedRef = "develop",
        Commit = Commit,
        FileSha = Blob,
        ByteSize = 2048,
    };

    private static FactEnvelope Carrying(IntentRead? read) => new()
    {
        IdempotencyKey = "flight:intent.read:one",
        Kind = FactKinds.IntentRead,
        Digest = new string('a', 64),
        ObservedAt = DateTimeOffset.UnixEpoch,
        IntentRead = read,
    };

    [Test]
    public async Task It_is_a_fact_kind()
    {
        await Assert.That(FactKinds.IntentRead).IsEqualTo("intent.read");
        await Assert.That(FactKinds.All).Contains(FactKinds.IntentRead);
    }

    [Test]
    public async Task A_fact_carrying_one_is_valid()
    {
        await Assert.That(FactEnvelope.Validate(Carrying(Read()))).IsNull();
    }

    [Test]
    public async Task The_kind_without_its_payload_is_refused()
    {
        await Assert.That(FactEnvelope.Validate(Carrying(null))).IsNotNull();
    }

    [Test]
    public async Task The_commit_and_the_file_are_object_ids()
    {
        await Assert.That(IntentRead.Validate(Read() with { Commit = "develop" })).IsNotNull()
            .Because("a ref here would be the very name that moves, which the fact exists to pin.");
        await Assert.That(IntentRead.Validate(Read() with { FileSha = "not-a-sha" })).IsNotNull();
    }

    [Test]
    public async Task The_size_is_not_negative_and_the_path_stays_inside()
    {
        await Assert.That(IntentRead.Validate(Read() with { ByteSize = -1 })).IsNotNull();
        await Assert.That(IntentRead.Validate(Read() with { Path = "../outside.md" })).IsNotNull();
    }

    [Test]
    public async Task It_round_trips_on_the_wire()
    {
        var json = JsonSerializer.Serialize(Carrying(Read()), JsonSerializerOptions.Web);
        var back = JsonSerializer.Deserialize<FactEnvelope>(json, JsonSerializerOptions.Web);

        await Assert.That(back!.IntentRead).IsEqualTo(Read());
    }
}
