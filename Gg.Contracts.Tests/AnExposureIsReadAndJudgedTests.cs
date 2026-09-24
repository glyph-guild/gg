using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// An exposure document is read from text, refused when it cannot say where
/// anything appears, and judged when it changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0027, the second step.</b> The role, its directory and its rendering
/// landed first. This is the half that makes the document usable: a reader, the
/// refusals that keep a broken one from ever being applied, and the comparator
/// that decides whether an edit needs somebody's decision.
/// </para>
/// <para>
/// <b>Why the comparator is not optional.</b> A role that ships without one
/// silently inherits the strategy's answer — <i>cannot be shown to tighten</i> —
/// which reads like a safe default and is not, because the ordering it produces
/// can still land ungated on the far side. It was one of the ten places the
/// measurement said no build would find, so it is written here first and the
/// implementation follows it.
/// </para>
/// <para>
/// <b>Every widening here is about who can be reached.</b> Growing the inventory
/// adds addresses; changing the hostname pattern moves every address at once, to
/// names nobody has registered anywhere; changing the credential pattern points
/// every slot at secrets nobody has placed; and changing the kind changes what is
/// dialled. None of those can be shown to reduce anything, so each takes a
/// decision. Shrinking the inventory is the one edit that only ever removes, and
/// it tightens.
/// </para>
/// </remarks>
public class AnExposureIsReadAndJudgedTests
{
    private const string Text = """
        kind: cloudflare-tunnel
        inventory:
          size: 8
          hostnames: jdapp-{slot}.example.dev
          credentials: local:exposure/jdapp-{slot}
        """;

    private static Exposure Eight() => new()
    {
        Kind = ExposureKinds.CloudflareTunnel,
        Inventory = new ExposureInventory
        {
            Size = 8,
            Hostnames = "jdapp-{slot}.example.dev",
            Credentials = "local:exposure/jdapp-{slot}",
        },
    };

    [Test]
    public async Task It_reads_back_what_it_says()
    {
        var read = EnvelopeYaml.ParseExposure(Text);

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(read.Exposure).IsNotNull();
        await Assert.That(read.Exposure!.Kind).IsEqualTo(ExposureKinds.CloudflareTunnel);
        await Assert.That(read.Exposure.Inventory.Size).IsEqualTo(8);
        await Assert.That(read.Exposure.Inventory.Hostnames).IsEqualTo("jdapp-{slot}.example.dev");
        await Assert.That(read.Exposure.Inventory.Credentials)
            .IsEqualTo("local:exposure/jdapp-{slot}");
    }

    [Test]
    public async Task What_it_reads_is_what_it_writes()
    {
        var round = EnvelopeYaml.ParseExposure(EnvelopeText.Render(Eight()));

        await Assert.That(round.Diagnosis).IsNull();
        await Assert.That(round.Exposure).IsEqualTo(Eight())
            .Because("pull renders and apply reads, so a document that did not survive the "
                   + "round trip would report a change nobody made on the very next diff.");
    }

    [Test]
    public async Task A_misspelt_key_is_refused_rather_than_read_as_absent()
    {
        var read = EnvelopeYaml.ParseExposure(Text.Replace("hostnames:", "hostname:"));

        await Assert.That(read.Exposure).IsNull();
        await Assert.That(read.Diagnosis).IsNotNull();
        await Assert.That(read.Diagnosis!).Contains("hostname")
            .Because("an absent hostnames and a misspelt one are different documents, and "
                   + "only one of them is a mistake somebody wants told about.");
    }

    [Test]
    public async Task A_provider_gg_cannot_drive_is_refused_where_the_document_is_written()
    {
        var read = EnvelopeYaml.ParseExposure(Text.Replace("cloudflare-tunnel", "ngrok"));
        var refusal = read.Exposure is null ? read.Diagnosis : Exposure.Validate(read.Exposure);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains(ExposureKinds.CloudflareTunnel)
            .Because("a refusal that does not name what IS available leaves somebody guessing "
                   + "at a vocabulary they cannot see.");
    }

    [Test]
    public async Task A_pattern_with_no_slot_is_refused_because_every_slot_would_be_one_address()
    {
        var shared = Eight() with
        {
            Inventory = Eight().Inventory with { Hostnames = "jdapp.example.dev" },
        };

        await Assert.That(Exposure.Validate(shared)).IsNotNull()
            .Because("eight slots sharing one hostname is the replica collision written into "
                   + "the document rather than reached by accident: a provider accepts the "
                   + "second connector and routes to whichever is nearer, so two flights serve "
                   + "each other's previews with no error anywhere.");
    }

    [Test]
    public async Task An_inventory_of_nothing_is_refused()
    {
        var empty = Eight() with { Inventory = Eight().Inventory with { Size = 0 } };

        await Assert.That(Exposure.Validate(empty)).IsNotNull()
            .Because("an exposure with no slots declares a place where nothing can appear, "
                   + "which is the same as not declaring one and harder to notice.");
    }

    [Test]
    public async Task Growing_the_inventory_widens()
    {
        var wider = Eight() with { Inventory = Eight().Inventory with { Size = 12 } };

        var moved = Exposure.Widening(Eight(), wider);

        await Assert.That(moved).IsNotNull();
        await Assert.That(moved!.Value.Field).IsEqualTo("inventory.size");
    }

    [Test]
    public async Task Shrinking_the_inventory_is_the_one_edit_that_tightens()
    {
        var narrower = Eight() with { Inventory = Eight().Inventory with { Size = 4 } };

        await Assert.That(Exposure.Widening(Eight(), narrower)).IsNull()
            .Because("fewer addresses is strictly less reach, and a tightening a tenant has to "
                   + "ask permission for is a tenant who stops tightening.");
    }

    [Test]
    public async Task Moving_the_hostnames_widens_because_nobody_has_registered_the_new_ones()
    {
        var elsewhere = Eight() with
        {
            Inventory = Eight().Inventory with { Hostnames = "jdapp-{slot}.other.dev" },
        };

        var moved = Exposure.Widening(Eight(), elsewhere);

        await Assert.That(moved).IsNotNull();
        await Assert.That(moved!.Value.Field).IsEqualTo("inventory.hostnames");
    }

    [Test]
    public async Task Moving_the_credentials_widens_and_changing_the_kind_does_too()
    {
        var resecreted = Eight() with
        {
            Inventory = Eight().Inventory with { Credentials = "local:exposure/other-{slot}" },
        };

        await Assert.That(Exposure.Widening(Eight(), resecreted)?.Field)
            .IsEqualTo("inventory.credentials");

        // Kind has one member today, so this is asserted through a value the
        // vocabulary does not hold: the comparator's job is to notice the field
        // moved, and it must not wait for a second member to start doing it.
        var redialled = Eight() with { Kind = "a-kind-from-a-later-version" };

        await Assert.That(Exposure.Widening(Eight(), redialled)?.Field).IsEqualTo("kind");
    }

    [Test]
    public async Task An_unchanged_document_has_not_moved()
    {
        // LIVENESS. Every assertion above asks for a non-null, and a comparator
        // that answered "widening" to everything would satisfy all of them.
        await Assert.That(Exposure.Widening(Eight(), Eight())).IsNull();
    }
}
