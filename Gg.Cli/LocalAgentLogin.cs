using Gg.Client;
using Gg.Local;
using Gg.Runner;

namespace Gg.Cli;

/// <summary>
/// Whether this machine will start its agent's login ceremony when a console
/// asks it to over the channel, decided by its own file and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own key, because this is the first SPAWNING verb.</b>
/// <c>accept-configured</c> lets a person put a secret on this machine; this
/// lets a person make this machine run a program that mints one. The
/// channel's own tests say a fourth verb must not inherit the third's gate
/// <i>"without somebody deciding it is enough"</i>, and the decision is that
/// it is not: a machine that agreed to be HANDED a token has not agreed to
/// START anything.
/// </para>
/// <para>
/// <b>Null unless the file says <c>accept-agent-login</c></b>, and then both
/// ports as one grant: the class that drives the agent's binary under a
/// pseudo-terminal, and the keeper that writes what it mints to the same
/// store the runner reads at launch. A member never asks this - its path
/// leaves the port null by construction - and takes its token by
/// <c>gg credential send --agent</c> instead.
/// </para>
/// </remarks>
public static class LocalAgentLogin
{
    public static AgentLoginPorts? For(
        Configuration? file, ICredentialStore store, ExecutorDeclaration declared)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(declared);

        return file?.AcceptAgentLogin is true
            ? new AgentLoginPorts(new SetupTokenLogin(declared.Binary), new LocalCredentialKeeper(store))
            : null;
    }
}
