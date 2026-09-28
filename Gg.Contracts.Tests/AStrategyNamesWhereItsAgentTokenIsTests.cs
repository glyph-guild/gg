using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A strategy may say where its members read their agent credential, because a
/// member cannot be handed one and its own store is emptied every twelve hours.
/// </summary>
/// <remarks>
/// <para>
/// <b>The last link in #747.</b> <c>GG_AGENT_LOCATOR</c> made a vault reference
/// sayable on a machine; nothing delivers it to a member. The two doors that
/// fill a member's credential store both write into the container, a member
/// token is not renewable, and the replacement has a different runner id — so
/// the login ceremony was a ceremony every twelve hours, for ever. Measured on
/// a live pool: three members in sequence, each holding for want of a login,
/// each ending on its credential having taken no work.
/// </para>
/// <para>
/// <b>It belongs to the POOL, which is why it is here and not on the root
/// envelope.</b> Which agent a pool's members run, and whose subscription pays
/// for it, is the same kind of fact as which image they are made from — <c>dev</c>
/// and <c>ui</c> can reasonably differ. A fleet-wide setting could not say that.
/// </para>
/// <para>
/// <b>It travels by the credential, not in the container.</b>
/// <c>MemberCredentialIssued</c> already carries the labels a member may
/// advertise, decided at mint from the strategy in force — <i>"everything the
/// credential will be is settled here, so redeeming decides nothing"</i>. A
/// locator settles the same way. The create body stays three environment
/// variables asserted byte for byte, which is the sign this is the right seam:
/// a member's environment is readable through the scope proxy for the life of
/// the container, so a name may ride there and a value never may.
/// </para>
/// <para>
/// <b>A reference, never a value, and the refusal never repeats it.</b> The slot
/// credential's rule (gg#701): a bare secret is refused at the door, and the
/// check runs before anything echoes what it was given, because the refusal is
/// exactly the moment somebody pasted a token where a name belongs and a
/// diagnosis quoting it would print the secret into a console and a flight log.
/// </para>
/// </remarks>
public class AStrategyNamesWhereItsAgentTokenIsTests
{
    private const string AVaultReference = "keyvault://a-vault.example.invalid/agent-claude";

    /// <summary>Token-shaped, and it must never appear in a refusal.</summary>
    private const string APastedToken =
        "sk-ant-oat01-ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";

    private static EnvironmentStrategy Plain() => new()
    {
        Kind = StrategyKinds.DockerHost,
        Environment = "ui",
        Inventory = new StrategyInventory { Pool = "gg-pool-ui", Size = 2, Warm = 2 },
        PullPoint = PullPoints.ResidentRunner,
        Image = "127.0.0.1:5000/gg-member-browser@sha256:"
              + "7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c",
        Bounds = new StrategyBounds { PoolMax = 2 },
    };

    private static EnvironmentStrategy Naming(string locator) =>
        Plain() with { AgentLocator = locator };

    // ---- what may be declared ----

    [Test]
    public async Task A_strategy_may_name_a_vault_reference()
    {
        await Assert.That(EnvironmentStrategy.Validate(Naming(AVaultReference))).IsNull()
            .Because("a member reads it with the identity it inherits from its host, which is "
                   + "the only credential a container replaced every twelve hours can keep.");
    }

    [Test]
    public async Task A_strategy_may_name_a_local_agent_locator()
    {
        await Assert.That(EnvironmentStrategy.Validate(
                Naming(CredentialLocator.ForAgent("claude")))).IsNull()
            .Because("a pool whose host has the file placed on it is a legitimate thing to "
                   + "declare, and refusing it would make the explicit form of the default "
                   + "an error.");
    }

    [Test]
    public async Task Absent_is_valid_and_is_every_strategy_written_before_this()
    {
        await Assert.That(EnvironmentStrategy.Validate(Plain())).IsNull()
            .Because("absent means the member derives the local file it always did. This adds a "
                   + "way to say something, and says nothing about strategies that do not.");

        await Assert.That(Plain().AgentLocator).IsNull();
    }

    // ---- what is refused ----

    [Test]
    public async Task A_pasted_token_is_refused()
    {
        await Assert.That(EnvironmentStrategy.Validate(Naming(APastedToken))).IsNotNull()
            .Because("this document is stored, versioned, rendered by gg airspace pull and read "
                   + "at a gate - a secret in it would be copied to all four.");
    }

    [Test]
    public async Task And_the_refusal_does_not_repeat_the_value()
    {
        var refused = EnvironmentStrategy.Validate(Naming(APastedToken));

        await Assert.That(refused!).DoesNotContain(APastedToken)
            .Because("gg#701's ordering, for its reason: the refusal is the moment a token was "
                   + "pasted where a name belongs, so quoting it would put the secret into the "
                   + "console that printed the refusal and the log that kept it.");
    }

    [Test]
    public async Task A_locator_outside_the_agent_namespace_is_refused()
    {
        // ForRepo reduces a slug through the same character set, so the two
        // derivations are disjoint only while one refuses the other's namespace.
        await Assert.That(EnvironmentStrategy.Validate(Naming("local:repo/jdx-jdnext")))
            .IsNotNull();
    }

    // ---- the document round-trips ----

    [Test]
    public async Task It_survives_being_written_and_read_back()
    {
        var text = EnvelopeText.Render(Naming(AVaultReference));

        await Assert.That(EnvelopeYaml.ParseStrategy(text).Strategy)
            .IsEqualTo(Naming(AVaultReference))
            .Because("gg airspace pull writes the document a person edits, and a member dropped "
                   + "on the way through is a pool that silently goes back to holding.");
    }

    [Test]
    public async Task A_strategy_that_names_none_writes_no_line_for_it()
    {
        var text = EnvelopeText.Render(Plain());

        await Assert.That(EnvelopeYaml.ParseStrategy(text).Strategy).IsEqualTo(Plain())
            .Because("absence is a document that did not say, and a pull that invented the key "
                   + "would put words in an author's mouth and then diff against them.");
    }
}
