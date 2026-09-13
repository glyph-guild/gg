using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A person can hand a runner a credential over the channel, and the control
/// plane cannot read it or carry it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The third ask kind, and it is the one ADR-0013 priced.</b>
/// <see cref="RunnerAskKinds"/> says so out loud: <i>Read-only, for now, and the
/// "for now" is recorded rather than implied. ADR-0013's amendment decides that
/// driving a runner is a flight, so this vocabulary is expected to grow — and
/// when it does it will be through a lease and an envelope, not by a third value
/// appearing here quietly.</i> This is that value, arriving loudly: a fingerprint
/// moves and a contract version is spent.
/// </para>
/// <para>
/// <b>Why there is no other way in.</b> Three doors are shut on purpose and each
/// is recorded. A strategy document cannot hold a credential
/// (<c>StrategyContainmentTests</c>, by shape rather than by string). An offered
/// configuration cannot carry one - <c>OfferableKeys</c> excludes the intent
/// readers because <i>their value IS a command line with a credential variable
/// in it</i>. And a pool member has no file anybody can reach:
/// <c>DockerPoolAdapter</c> creates members with no binds, which is why
/// <c>accept-unattended</c> was retired as <i>a switch for a machine that could
/// not reach it</i>. What is left is the image, rebuilt for every rotation, or
/// this.
/// </para>
/// <para>
/// <b>THIS IS THE ONE TYPE ON THE CONTRACT THAT MAY HOLD A SECRET, and the
/// whole of its safety is that nothing can carry it to the control plane.</b>
/// Article VIII is not bent by it: the control plane stores <i>references and
/// facts, never secrets</i>, and a sealed peer-to-peer value it never sees is
/// the aligned answer rather than the transgressive one. The guarantee is
/// structural and asserted below - no endpoint names this type, so there is no
/// request body it can enter; and the answer that comes back has nowhere to put
/// one, which is <c>CredentialContainmentTests</c>' own technique pointed at the
/// return direction.
/// </para>
/// <para>
/// <b>It does not widen what a runner may be told to DO.</b>
/// <c>RunnerAskClosureTests</c> plants <c>RunCommandAsk</c> by name because
/// ADR-0013 names it, and this is deliberately not that: it carries a credential
/// for the runner's OWN configuration, performs nothing, returns no data, and is
/// answerable only inside a lease. The burden of saying so is on this change.
/// </para>
/// </remarks>
public class ARunnerCanBeGivenACredentialTests
{
    /// <summary>The one control character, spelled rather than typed.</summary>
    /// <remarks>
    /// The discipline <c>RunnerSaidIsStrippedTests</c> holds: a file that
    /// composes a sequence out of a literal control character is one nobody can
    /// review by reading, and one that survives a copy and paste into somewhere
    /// it should not.
    /// </remarks>
    private const string Escape = "\u001b";

    [Test]
    public async Task The_third_kind_is_registered_rather_than_present()
    {
        // THE FOUR REGISTRATIONS RunnerAskVocabularyTests HOLDS, for the value
        // that is new. A kind absent from All is a kind no fingerprint covers -
        // which is the whole mechanism that makes widening this channel cost a
        // contract version rather than a commit.
        await Assert.That(RunnerAskKinds.All).Contains(RunnerAskKinds.ConfigureCredential);

        await Assert.That(typeof(ConfigureCredentialAsk).GetCustomAttribute<PinnedIdAttribute>())
            .IsNotNull();

        await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(typeof(ConfigureCredentialAsk)))
            .IsTrue();

        await Assert.That(typeof(RunnerAsk).GetProperty("ConfigureCredential")).IsNotNull()
            .Because("the kind says which slot is filled, and a kind with no slot cannot be "
                   + "constructed - which is the third registration doing its work.");
    }

    [Test]
    public async Task Its_purpose_is_a_value_rather_than_a_free_string()
    {
        // RunnerCapabilityPurposes WAS CLOSED AT ONE, and the closure's own
        // remark says a second value is exactly the change that should cost a
        // fingerprint: "widening what a capability authorises is the change that
        // turns an introduction into a standing grant".
        await Assert.That(RunnerCapabilityPurposes.All)
            .Contains(RunnerCapabilityPurposes.ConfigureThisRunner);

        await Assert.That(RunnerCapabilityPurposes.ConfigureThisRunner)
            .IsNotEqualTo(RunnerCapabilityPurposes.TailYourOwnLog)
            .Because("an introduction minted to read a log must not also place a credential.");
    }

    [Test]
    public async Task No_endpoint_can_carry_it()
    {
        // THE WHOLE SAFETY ARGUMENT, AS A PROPERTY. A secret on the contract is
        // only safe while nothing can send it to the control plane, and the way
        // to be sure is that no route names it - not that whoever writes the
        // next route remembers. The channel has its own serializer context for
        // the same reason: "keeping them apart means a type added to one is not
        // silently serialisable over the other".
        foreach (var endpoint in ProtocolSurface.Endpoints)
        {
            foreach (var carried in (Type?[])
                [endpoint.Request, endpoint.Response, endpoint.PendingResponse])
            {
                await Assert.That(Reaches(carried, typeof(ConfigureCredentialAsk))).IsFalse()
                    .Because($"{endpoint.Method} {endpoint.Path} can carry a secret to the "
                           + "control plane, which is Article VIII by a different route.");
            }
        }
    }

    [Test]
    public async Task Nothing_comes_back_that_could_hold_the_secret()
    {
        // CredentialContainmentTests' TECHNIQUE, POINTED THE OTHER WAY. A
        // runner that echoed what it was given - in an acknowledgement, in a
        // diagnosis - would put the secret on a channel a person is watching and
        // into whatever renders it. The answer names the locator, which is a
        // reference, and has nowhere to put a value.
        var members = typeof(ConfiguredCredential).GetProperties();

        foreach (var member in members)
        {
            foreach (var word in (string[])
                ["secret", "token", "password", "passphrase", "bearer", "apikey"])
            {
                await Assert.That(member.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    .IsFalse()
                    .Because($"'{member.Name}' is named for secret material on the way BACK.");
            }

            await Assert.That(member.PropertyType == typeof(string)
                           || member.PropertyType == typeof(bool)
                           || member.PropertyType == typeof(DateTimeOffset))
                .IsTrue()
                .Because($"'{member.Name}' is a {member.PropertyType.Name} - a free-form "
                       + "container carries a secret while passing a name check.");
        }

        await Assert.That(members.Length).IsLessThanOrEqualTo(3)
            .Because("the member set is closed, so a fourth member is a decision somebody "
                   + "makes here rather than one that arrives.");
    }

    [Test]
    public async Task What_comes_back_is_stripped_like_everything_else()
    {
        // THE RULE IS ONE RULE FOR BOTH SIDES. A locator echoed home is text
        // from a machine the ADR calls hostile, about to be rendered in a
        // terminal, and Stripped() is applied at ingress so every surface
        // inherits the property instead of re-deriving it. An arm added to the
        // envelope and forgotten here is an escape sequence riding home.
        var said = new RunnerSaid
        {
            Kind = RunnerAskKinds.ConfigureCredential,
            Configured = new ConfiguredCredential
            {
                Locator = $"local:acme/widgets{Escape}[31m",
                Written = true,
            },
        }.Stripped();

        await Assert.That(said.Configured!.Locator).DoesNotContain(Escape);
    }

    /// <summary>Whether a wire type reaches another through its members.</summary>
    /// <remarks>
    /// Transitively, because a type that carries a secret one envelope down is
    /// carrying it just as surely as one that names it directly.
    /// </remarks>
    private static bool Reaches(Type? from, Type wanted, HashSet<Type>? seen = null)
    {
        if (from is null || from == wanted)
        {
            return from is not null;
        }

        seen ??= [];

        if (!seen.Add(from) || from.Namespace?.StartsWith("Gg.Contracts", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return from.GetProperties().Any(p =>
            Reaches(p.PropertyType, wanted, seen)
            || (p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericArguments().Any(a => Reaches(a, wanted, seen))));
    }
}
