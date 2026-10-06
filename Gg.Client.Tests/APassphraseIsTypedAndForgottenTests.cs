using System.Reflection;
using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The passphrase is typed, and nothing keeps it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.3-02, and rule 2 of the slice asserted over the shape of the code
/// rather than read off it.</b> The reason is already written down in
/// <c>Credentials.cs</c>, where an environment-variable credential kind is
/// refused: <i>"environment variables leak into child processes, `ps` output,
/// crash dumps and CI logs ... and 'we will be careful' is not a control."</i> A
/// passphrase is worse than a credential in one respect — it opens every
/// credential a person holds, not one.
/// </para>
/// <para>
/// <b>Three places it must not be, and each is checked differently.</b> Not an
/// argument, because <c>argv</c> is world-readable on a shared machine and lands
/// in shell history. Not an environment variable, for the sentence above. And not
/// a field or property on anything that outlives the call, because
/// <c>AppState</c> is serialized to disk under <c>GG_STATE_DUMP</c> and a console
/// is the other caller of this path.
/// </para>
/// <para>
/// <b>Asserted structurally because the behavioural version cannot see it.</b> A
/// test that pushed a credential and then looked for the passphrase somewhere
/// would pass while the field existed and happened to be empty. The shape is the
/// thing: no member of the type may hold it, so there is nowhere for it to be.
/// </para>
/// </remarks>
public class APassphraseIsTypedAndForgottenTests
{
    /// <summary>Every type on the push path that a passphrase passes through.</summary>
    /// <remarks>
    /// <b>HAND-WRITTEN, so it does not notice a new one.</b> That is the usual cost
    /// of a list like this and it is paid here because the alternative — scanning
    /// every type in the assembly for a member that looks like a secret — is what
    /// <c>CredentialContainmentTests</c> already does for the contract. These are
    /// the types this step touched.
    /// </remarks>
    private static readonly Type[] OnThePath =
        [typeof(SendACredential), typeof(SendACredential.ToSend), typeof(CredentialCommands)];

    [Test]
    public async Task No_type_on_the_push_path_has_a_member_that_could_hold_it()
    {
        var offenders = new List<string>();

        foreach (var type in OnThePath)
        {
            foreach (var member in type.GetMembers(
                         BindingFlags.Public | BindingFlags.NonPublic
                       | BindingFlags.Instance | BindingFlags.Static))
            {
                if (member is not (PropertyInfo or FieldInfo))
                {
                    continue;
                }

                var name = member.Name.ToLowerInvariant();

                if (name.Contains("passphrase", StringComparison.Ordinal)
                 || name.Contains("password", StringComparison.Ordinal))
                {
                    offenders.Add($"{type.Name}.{member.Name}");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("a passphrase opens every credential a person holds. It is read, used, and "
                   + "goes out of scope; a member able to hold one is a place it can outlive the "
                   + "push, and AppState is written to disk under GG_STATE_DUMP.");
    }

    [Test]
    public async Task It_is_not_an_argument_to_any_public_entry_point()
    {
        // NOT IN argv, which is world-readable on a shared machine and lands in
        // shell history. The person's own key adapter takes one - PersonKey.Unlock
        // must, it is the thing that unwraps - and that is the ONE place, reached
        // from a prompt rather than from a command line.
        var offenders = new List<string>();

        foreach (var type in OnThePath)
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                foreach (var parameter in method.GetParameters())
                {
                    var name = (parameter.Name ?? "").ToLowerInvariant();

                    if (name.Contains("passphrase", StringComparison.Ordinal)
                     || name.Contains("password", StringComparison.Ordinal))
                    {
                        offenders.Add($"{type.Name}.{method.Name}({parameter.Name})");
                    }
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("every one of these is reachable from a CLI verb, so a parameter is one "
                   + "`--passphrase` flag away from being in somebody's shell history.");
    }

    [Test]
    public async Task And_no_environment_variable_is_read_for_one()
    {
        // THE SENTENCE Credentials.cs ALREADY WROTE, held over the source of the
        // files this step changed. A scan rather than a behaviour, because an
        // Environment.GetEnvironmentVariable that is never reached today is one a
        // later change makes reachable without anybody noticing.
        var root = RepoRoot();

        foreach (var file in (string[])
                 ["Gg.Client/SendACredential.cs", "Gg.Client/PersonKey.cs",
                  "Gg.Client/CredentialCommands.cs"])
        {
            var text = await File.ReadAllTextAsync(Path.Combine(root, file));

            foreach (var line in text.Split('\n'))
            {
                if (!line.Contains("GetEnvironmentVariable", StringComparison.Ordinal))
                {
                    continue;
                }

                await Assert.That(line.ToLowerInvariant()).DoesNotContain("pass")
                    .Because($"{file} reads an environment variable that looks like a passphrase, "
                           + "and environment variables leak into child processes, `ps` output, "
                           + "crash dumps and CI logs. Line: " + line.Trim());
            }
        }
    }

    private static string RepoRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        return (at ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory))
            .FullName;
    }
}
