using System.Reflection;
using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// A passphrase typed in the console reaches no serialized state, so it cannot land in a
/// diagnostics bundle.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.6-05, and the rule that actually applies — which is not the one first given.</b>
/// The broadcast was deferred out of the console on the grounds that a passphrase could
/// not be read there at all. That was wrong: <c>TextField.Secret</c> masks input, and the
/// owner said so. What is true is narrower and sharper: <c>AppState</c> is serialized to
/// JSON, written to disk under <c>GG_STATE_DUMP</c> and fed to the diagnostics bundle.
/// </para>
/// <para>
/// <b>A field's text lives in a VIEW, and views are not the source of truth.</b>
/// <c>UiOutcome(Command Exit, AppState State)</c> is the only survivor of a session, so a
/// secret typed into a field puts nothing in the dump — provided nothing copies it into
/// the state on the way past. That proviso is what this file is.
/// </para>
/// <para>
/// <b>Asserted over the SHAPE, because the behavioural version cannot see it.</b> A test
/// that typed a passphrase and then searched a dump would pass while a member existed and
/// happened to be empty, and the member is the hole whether or not today's code fills it.
/// </para>
/// <para>
/// <b>What crosses to the push instead is the OPENER.</b> A controller held outside the
/// store unlocks the key and hands over something that performs agreements rather than
/// something that is a secret — which is the mechanism the architecture already names for
/// a non-serializable handle, and which gives one passphrase for the whole audience for
/// free.
/// </para>
/// </remarks>
public class APassphraseTypedInTheConsoleIsNotInTheDumpTests
{
    [Test]
    public async Task No_member_of_the_state_could_hold_a_passphrase()
    {
        var offenders = Members(typeof(AppState))
            .Where(name => name.Contains("passphrase", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("password", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("secret", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("AppState is written to disk under GG_STATE_DUMP and goes into the diagnostics "
                   + "bundle, so a member able to hold a passphrase is a passphrase in a support "
                   + "attachment. Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task Nor_could_any_type_the_state_carries_for_this_act()
    {
        // THE ROW IS THE THING THE STATE HOLDS, so its shape matters as much as
        // AppState's: a member added there is serialized just the same.
        var offenders = Members(typeof(CredentialAudienceRow))
            .Where(name => name.Contains("passphrase", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("password", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("secret", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("every member of an audience row is a reference - a label, a locator, a flag. "
                   + "Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_state_carries_no_opener_either()
    {
        // NOT EVEN THE UNLOCKED KEY. An IAgreeAsAHolder is not a secret and would not
        // serialize to anything useful - but AppState must stay plain JSON-serializable
        // data, which is what makes terminal release possible, and a handle on it is the
        // architectural constraint this console is built on.
        var offenders = typeof(AppState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.Name.Contains("IAgreeAsAHolder", StringComparison.Ordinal)
                     || p.PropertyType.Name.Contains("PersonKey", StringComparison.Ordinal)
                     || p.PropertyType.Name.Contains("ToSend", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("state is fully serializable with every non-serializable handle on a "
                   + "controller OUTSIDE the store, and that is an architectural constraint rather "
                   + "than a style choice. Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task And_the_state_still_round_trips_as_JSON()
    {
        // THE PROPERTY ALL OF THE ABOVE PROTECTS. If the audience could not survive a
        // serialize-and-read, terminal release would lose the review a person was
        // halfway through - which is the one thing AppState exists to make possible.
        var state = new AppState
        {
            ActiveTab = TabId.Credentials,
            Mode = UiMode.CredentialAudience,
            AudienceFor = "local:acme/widgets",
            Audience =
            [
                new CredentialAudienceRow(
                    RunnerId: "r1", Label: "vmlinux001", Locator: "local:acme/widgets",
                    Declared: true, Reported: false, Reachable: true, Through: null),
            ],
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            state, AppStateJsonContext.Default.AppState);

        var read = System.Text.Json.JsonSerializer.Deserialize(
            json, AppStateJsonContext.Default.AppState)!;

        await Assert.That(read.Audience.Count).IsEqualTo(1)
            .Because("the review survives a session boundary, which is what terminal release needs "
                   + "and what a source-generated context has to be told about.");

        await Assert.That(read.AudienceFor).IsEqualTo("local:acme/widgets");
    }

    private static IReadOnlyList<string> Members(Type type) =>
        [.. type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                          | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m is PropertyInfo or FieldInfo)
            .Select(m => m.Name)];
}
