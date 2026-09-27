using Gg.Contracts;
using Gg.Local;
namespace Gg.Runner.Execution;

/// <summary>
/// Which executor this machine has, read from its environment.
/// </summary>
/// <remarks>
/// <para>
/// <b>Configured, like the vcs and destination adapters beside it.</b> Which agent
/// binary a machine has is deployment knowledge: <c>gg</c> is public and
/// distributed, and an operator who has not installed one has not installed one.
/// </para>
/// <para>
/// <b>Null is the ordinary state and not a degraded one.</b> A runner with no
/// executor does what every runner did before an executor existed - materialize,
/// extract, ship - and a flight that declares no loop was never going to invoke
/// anything anyway.
/// </para>
/// <para>
/// <b>This existing is the fix.</b> Until it did, <c>ClaudeCodeExecutor</c> was
/// constructed nowhere outside the test assemblies: <c>RunnerHost</c> took no
/// executor and the CLI passed none, so every runner the product started built a
/// loop whose executor was null and no flight ever invoked an agent. The seventh
/// instance of <i>registered is not invoked</i>, and the one that made every
/// question about what a loop may do unanswerable, because no loop ran.
/// </para>
/// </remarks>
public static class ExecutorConfiguration
{
    /// <summary>Which agent this machine has and where, when it has one.</summary>
    /// <remarks>
    /// Spelled in <see cref="ExecutorDeclaration"/>, which is also what parses
    /// it: a bare path is claude, <c>agent=path</c> names one, and the doctor
    /// reads it through the same parser from a project that cannot see this one.
    /// </remarks>
    public const string BinaryVariable = ExecutorDeclaration.Variable;

    /// <summary>The executor this machine is configured for, or null for none.</summary>
    /// <remarks>
    /// <b>Built WITH the trackers this runner can read</b>, because an executor
    /// that had them and was never given them is the shape this whole slice
    /// exists to remove. One place reads the environment; nothing downstream
    /// reads it again and reaches a different answer.
    /// </remarks>
    /// <param name="readers">The trackers this runner can read, or null to read the environment.</param>
    /// <param name="secretFor">This machine's credential lookup, for a tool server's credential.</param>
    /// <param name="declaration">
    /// The declaration, or null to read the environment. A composition root
    /// passes what <c>Settings</c> resolved - the environment, then the file -
    /// because that is what the doctor reports, and for a release the runner
    /// read only the first half of it.
    /// </param>
    public static IExecutorPort? FromEnvironment(
        IReadOnlyList<IntentReader>? readers = null,
        Func<string, string?>? secretFor = null,
        string? declaration = null,
        string? locator = null) =>
        // THE CHOICE IS MADE HERE, IN THE DEFAULT, which is where the vcs and
        // destination seams learned it has to be: "the adapterFor parameter
        // was passed only from tests, which is the same bug one layer up".
        // ExecutorDeclaration.Known has one member, so this is not a switch
        // yet - and the day it is, the second arm is here rather than in a
        // caller that assumed the first.
        ExecutorDeclaration.ParseOrNull(
            declaration ?? Environment.GetEnvironmentVariable(BinaryVariable), BinaryVariable)
            is { } declared
            ? new ClaudeCodeExecutor(
                declared.Binary,
                readers ?? IntentConfiguration.FromEnvironment(),
                secretFor,
                // THE ONE PLACE, again. How this process re-execs itself is a
                // process fact rather than configuration, so it cannot drift
                // between reads - but it is resolved here anyway, beside the
                // trackers, because an executor that had it and was never given
                // it is the shape this type exists to remove.
                SelfInvocation.Current,
                // AND HOW ITS AGENT AUTHENTICATES, from the same parse, so a
                // runner cannot pick an executor for one agent and an adapter
                // for another.
                AgentFor(declared, LocatorOr(locator)))
            : null;

    /// <summary>
    /// How to build the agent for ONE sweep, or null where this machine
    /// declares no executor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Per sweep, because the servers are per sweep.</b> A flight's executor
    /// is composed once with every tracker this runner can read and one way of
    /// starting its own server; a sweep is launched with exactly one tracker -
    /// bound to that watch's reviewed query - and with the server in sweep
    /// mode. Building the executor per sweep is what keeps
    /// <c>SweepLauncher</c>'s decision from being reimplemented as a second
    /// reader list somewhere else.
    /// </para>
    /// <para>
    /// <b>The choice of adapter stays here</b>, with the flight path's, for the
    /// reason <see cref="FromEnvironment"/> records: the day
    /// <c>ExecutorDeclaration.Known</c> has a second member, the second arm is
    /// written in this file rather than in a caller that assumed the first.
    /// </para>
    /// </remarks>
    /// <param name="secretFor">This machine's credential lookup, for the agent's own token.</param>
    /// <param name="declaration">
    /// The declaration, or null to read the environment - so a value in the
    /// configuration file reaches this as surely as a variable does.
    /// </param>
    public static Func<IntentReader, SelfInvocation, IExecutorPort>? ForSweeps(
        Func<string, string?>? secretFor = null, string? declaration = null,
        string? locator = null) =>
        ExecutorDeclaration.ParseOrNull(
            declaration ?? Environment.GetEnvironmentVariable(BinaryVariable), BinaryVariable)
            is { } declared
            ? (reader, self) => new ClaudeCodeExecutor(
                declared.Binary,
                // ONE TRACKER: the watch's, and a sweep names no other.
                [reader],
                secretFor,
                // IN SWEEP MODE, decided by the launcher rather than here.
                self,
                AgentFor(declared, LocatorOr(locator)))
            : null;

    /// <summary>Where this machine's agent credential is named, when it is.</summary>
    /// <remarks>
    /// <b>Its own variable rather than a second field on
    /// <see cref="BinaryVariable"/>.</b> That one is held to a shape by
    /// <c>ExecutorDeclaration</c> and by a scan that admits exactly one reader;
    /// hanging a locator off it would overload one line with two unrelated
    /// answers, and the ratchet that keeps the binary honest would then be
    /// guarding a credential too.
    /// </remarks>
    public const string LocatorVariable = "GG_AGENT_LOCATOR";

    /// <summary>How this machine's agent authenticates, or null for none - from the environment.</summary>
    /// <remarks>
    /// A second read of the same variable through the same parser, for the
    /// composition root that needs the adapter beside the executor and cannot
    /// reach into one to ask.
    /// </remarks>
    /// <param name="declaration">
    /// The declaration, or null to read the environment - the same one the
    /// executor beside it was handed, or the two disagree about which agent
    /// this machine has.
    /// </param>
    /// <param name="credential">
    /// Where the agent's credential is kept, or null to read the environment. Passed
    /// at every entry point that builds an agent rather than resolved inside
    /// <see cref="AgentFor"/>, so this file keeps its rule that one place reads
    /// the environment and nothing downstream reaches a different answer.
    /// </param>
    public static IAuthenticateAnAgent? AgentFromEnvironment(
        string? declaration = null, string? locator = null) =>
        ExecutorDeclaration.ParseOrNull(
            declaration ?? Environment.GetEnvironmentVariable(BinaryVariable), BinaryVariable) is { } declared
            ? AgentFor(declared, LocatorOr(locator))
            : null;

    /// <summary>The declared credential, or what the environment says.</summary>
    /// <remarks>
    /// <b>Three entry points build an agent and all three must resolve this.</b>
    /// The flight path, the sweep path and the adapter-only path each read the
    /// binary declaration for themselves; an agent credential resolved in only
    /// one of them is a machine whose held runner reads a vault and whose flights
    /// read a file that is not there. The slot credential's lesson, one seam over:
    /// <i>wired at all three composition roots - a missed one serves no preview
    /// and says nothing</i>.
    /// </remarks>
    private static string? LocatorOr(string? locator) =>
        locator ?? Environment.GetEnvironmentVariable(LocatorVariable);

    /// <summary>
    /// The locator an agent's credential is read from: the declared one, or the
    /// local file every machine has always used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Absent is the default and not a missing answer.</b> Every machine with
    /// a token already placed must go on deriving the same file, so this is a way
    /// to name somewhere else rather than a change to where the default is.
    /// </para>
    /// <para>
    /// <b>Two shapes are admitted and nothing else.</b> A
    /// <c>keyvault://</c> reference is read by this machine's managed identity,
    /// and a <c>local:</c> locator in the agent namespace is the file. A local
    /// locator outside that namespace is refused because <c>ForRepo</c> reduces a
    /// slug through the same character set - the two derivations are disjoint
    /// only while one refuses the other's namespace.
    /// </para>
    /// <para>
    /// <b>And the refusal never repeats the value.</b> This is exactly the moment
    /// somebody has pasted a token where a name belongs, so a diagnosis quoting
    /// it would put the secret into a console and, from there, a flight log. The
    /// variable's name is the only part safe to print.
    /// </para>
    /// </remarks>
    public static string LocatorFor(string? declaredLocator, string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        if (string.IsNullOrWhiteSpace(declaredLocator))
        {
            return CredentialLocator.ForAgent(provider);
        }

        var declared = declaredLocator.Trim();

        if (KeyVaultReference.Names(declared))
        {
            try
            {
                // PARSED HERE RATHER THAN AT THE FIRST PROBE. A reference that
                // cannot be read leaves a machine holding for ever with a
                // diagnosis about a vault, when what is wrong is the line
                // somebody typed.
                _ = KeyVaultReference.Parse(declared);
            }
            catch (Exception malformed) when (malformed is FormatException or ArgumentException)
            {
                throw Refused(
                    $"names a {KeyVaultReference.Scheme} reference that cannot be read: "
                  + malformed.Message);
            }

            return declared;
        }

        if (declared.StartsWith(CredentialLocator.LocalPrefix, StringComparison.Ordinal))
        {
            if (CredentialLocator.Validate(declared) is { } refused)
            {
                throw Refused($"names a local locator that is not well formed: {refused}");
            }

            var agents = $"{CredentialLocator.LocalPrefix}{CredentialLocator.AgentSegment}/";
            return declared.StartsWith(agents, StringComparison.Ordinal)
                ? declared
                : throw Refused(
                    $"names a local locator outside '{agents}', which is where an agent's own "
                  + "credential lives. A repository's locator reduces through the same character "
                  + "set, so reading one here would read the credential a tracker owns.");
        }

        // NOT QUOTED, and this is the arm that matters: a value rather than a
        // name is what a paste looks like.
        throw Refused(
            $"is neither a {KeyVaultReference.Scheme} reference nor a "
          + $"'{CredentialLocator.LocalPrefix}' locator. It says WHERE the credential is, never "
          + "what it is - put the secret in a vault, or run `gg credential add`, and name it here.");
    }

    private static InvalidOperationException Refused(string what) =>
        new($"{LocatorVariable} {what}");

    /// <summary>How the declared agent authenticates.</summary>
    /// <remarks>
    /// The choice is here, in the default, for the reason the executor's is.
    /// <see cref="ExecutorDeclaration.Parse"/> has already refused an agent
    /// nobody has an adapter for, so the arm below is unreachable by
    /// construction - and it throws rather than defaults, because "unknown
    /// means claude" is the assumption this whole declaration exists to end.
    /// </remarks>
    public static IAuthenticateAnAgent AgentFor(
        ExecutorDeclaration declared, string? locator = null)
    {
        ArgumentNullException.ThrowIfNull(declared);

        return declared.Agent switch
        {
            ExecutorDeclaration.Claude => new ClaudeAgentAuthentication(
                declared.Binary,
                locator: LocatorFor(locator, ExecutorDeclaration.Claude)),
            var other => throw new InvalidOperationException(
                $"'{other}' is an agent ExecutorDeclaration admits and this build has no "
              + "adapter for. The two lists have drifted; add the adapter here."),
        };
    }
}
