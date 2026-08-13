using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Write is possible only because two independent controls both hold.
/// </summary>
/// <remarks>
/// <para>
/// Slice one asserted the runner's repo access is read-only. This is that
/// changing, and <b>how it changes is the whole point</b>:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>The envelope declares a destination</b> - the tenant, in the control
/// plane, granting this flight permission to land somewhere.
/// </description></item>
/// <item><description>
/// <b>The credential carries write scope</b> - the developer, in their own
/// store, granting the ability to actually do it.
/// </description></item>
/// </list>
/// <para>
/// Neither is sufficient and <b>the envelope cannot widen the credential</b>. A
/// write destination against a read-only credential fails at the credential,
/// which is the layering model reaching across the boundary: a control plane
/// able to escalate a credential would make the customer's own store advisory.
/// </para>
/// </remarks>
public class DestinationWriteTests
{
    /// <summary>
    /// A file's CODE, with its comments removed.
    /// </summary>
    /// <remarks>
    /// The rule is about forcing a push, not about mentioning one - and the
    /// place you most want to write the word is the comment explaining that
    /// nothing does. Same exclusion as the terminal scan, asserted below so it
    /// is deliberate rather than convenient.
    /// </remarks>
    private static string CodeOf(string file) =>
        string.Join('\n', File.ReadAllLines(file)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)));

    private static IEnumerable<string> RunnerSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory
            .EnumerateFiles(Path.Combine(root.FullName, "Gg.Runner"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }

    // ---- the read port is still provably incapable ----

    [Test]
    public async Task The_read_adapter_still_has_no_write_method()
    {
        // Slice one's assertion, UNCHANGED and still passing. Write arrived as a
        // separate port rather than as methods on this one, so "the read path
        // cannot write" stayed a property of a type rather than becoming an
        // argument about which methods a caller happens to call.
        var members = typeof(IVcsAdapter).GetMembers().Select(m => m.Name).ToList();

        foreach (var writing in (string[])["Push", "PushAsync", "Commit", "OpenPullRequest", "Land"])
        {
            await Assert.That(members).DoesNotContain(writing);
        }

        await Assert.That(members).Contains(nameof(IVcsAdapter.CloneAsync))
            .Because("the scan has to be looking at the port that does the reading.");
    }

    [Test]
    public async Task Landing_lives_behind_a_port_of_its_own()
    {
        // The existence of this interface IS the declared escalation. A test
        // that only said "the read port cannot write" would pass in a world
        // where writing happened somewhere nobody had declared.
        var members = typeof(IDestinationAdapter).GetMembers().Select(m => m.Name).ToList();

        // TWO methods now, and that is the shape rather than an accident: the control
        // plane grants two permissions, so the port offers two calls and the runner
        // cannot conflate them. A single LandAsync that decided internally whether to
        // propose would put the gate decision inside the runner.
        await Assert.That(members).Contains(nameof(IDestinationAdapter.PushAsync));
        await Assert.That(members).Contains(nameof(IDestinationAdapter.ProposeAsync));
    }

    // ---- nothing is ever force-pushed ----

    [Test]
    public async Task The_push_plan_cannot_overwrite_a_branch()
    {
        // Asserted on the PLAN rather than on the outcome, because that single
        // leading '+' is the difference between creating a branch and
        // destroying whatever was there - and a plan is checkable without a
        // remote.
        var plan = GitInvocation.Push("https://forge.example/acme/widgets.git", "HEAD", "gg/GG-42", "s3cret");

        await Assert.That(plan.Arguments).DoesNotContain("--force");
        await Assert.That(plan.Arguments).DoesNotContain("-f");
        await Assert.That(plan.Arguments).DoesNotContain("--force-with-lease")
            .Because("a lease makes an overwrite safe against a race. It does not make it not an "
                   + "overwrite.");

        await Assert.That(plan.Arguments).Contains("HEAD:refs/heads/gg/GG-42");
        await Assert.That(plan.Arguments.Any(a => a.StartsWith('+'))).IsFalse()
            .Because("a leading + on the refspec forces the push without saying so anywhere a "
                   + "reader would look.");
    }

    [Test]
    public async Task The_secret_never_reaches_the_push_argument_list()
    {
        // argv is readable by every process on the machine. Same rule as the
        // fetch, and worth its own assertion because this is a second command
        // and the rule is easy to hold in one place only.
        var plan = GitInvocation.Push("https://forge.example/acme/widgets.git", "HEAD", "gg/GG-42", "s3cret");

        await Assert.That(plan.Arguments.Any(a => a.Contains("s3cret", StringComparison.Ordinal)))
            .IsFalse();
        await Assert.That(plan.Environment.Values.Any(v => v.Contains("s3cret", StringComparison.Ordinal)))
            .IsTrue()
            .Because("it travels in the child's environment, which is owner-readable at best.");
    }

    [Test]
    public async Task No_source_in_the_runner_can_force_a_push()
    {
        // Structural, over the whole project. A plan that cannot force is one
        // thing; a second code path that shells out with --force is what this
        // catches.
        var offenders = RunnerSources()
            .Where(f => CodeOf(f).Contains("--force", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("nothing this platform does may overwrite a branch. Found: "
                   + string.Join(", ", offenders));

        // The exclusion, asserted rather than assumed.
        var explained = Path.GetTempFileName();
        try
        {
            File.WriteAllText(explained, "// there is no --force here\nvar x = 1;\n");
            await Assert.That(CodeOf(explained).Contains("--force", StringComparison.Ordinal)).IsFalse();

            File.WriteAllText(explained, "// none here\nrun(\"--force\");\n");
            await Assert.That(CodeOf(explained).Contains("--force", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            File.Delete(explained);
        }
    }

    // ---- the credential used is the one that was granted ----

    /// <summary>
    /// A flight authenticates with the credential it was granted, and with
    /// nothing else the machine happens to hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Found by measurement, not by reading.</b> A push carrying a
    /// deliberately invalid secret SUCCEEDED against a real remote: git treats
    /// <c>credential.helper</c> as a list, our helper answered first and was
    /// rejected, and git then fell through to the developer's keychain - and
    /// authenticated, and pushed, and stored the credential back.
    /// </para>
    /// <para>
    /// That is the two-control design defeated from underneath. Ability is meant
    /// to come from what the developer registered for THIS repository; a git that
    /// can substitute any credential on the machine makes the registered one
    /// advisory, which is the property this platform sells.
    /// </para>
    /// </remarks>
    [Test]
    public async Task A_flight_cannot_authenticate_with_a_credential_the_machine_happens_to_hold()
    {
        foreach (var plan in (GitInvocation[])
                 [
                     GitInvocation.Push("https://forge.example/acme/widgets.git", "HEAD", "gg/GG-42", "s"),
                     GitInvocation.Fetch("https://forge.example/acme/widgets.git", "refs/heads/main", "s"),
                 ])
        {
            var helpers = plan.Arguments
                .Where(a => a.StartsWith("credential.helper=", StringComparison.Ordinal))
                .ToList();

            await Assert.That(helpers[0]).IsEqualTo("credential.helper=")
                .Because("an empty value RESETS git's helper list, and it has to come first - ours "
                       + "appended to a keychain means git tries the keychain too.");
            await Assert.That(helpers.Count).IsEqualTo(2)
                .Because("the reset, then ours, and nothing else.");
        }
    }

    [Test]
    public async Task The_ambient_git_configuration_is_refused_including_the_one_that_is_easy_to_miss()
    {
        // GIT_CONFIG_SYSTEM replaces one path. The macOS command-line tools ship
        // a SECOND system-level gitconfig it does not cover, and that file is
        // where the keychain helper comes from - so NOSYSTEM is the line that
        // actually closes this, and it is asserted separately because deleting it
        // as a duplicate is the obvious mistake.
        var run = RunnerSources().Single(f => Path.GetFileName(f) == "GitInvocation.cs");
        var code = CodeOf(run);

        await Assert.That(code).Contains("GIT_CONFIG_NOSYSTEM");
        await Assert.That(code).Contains("GIT_CONFIG_GLOBAL");
        await Assert.That(code).Contains("GIT_TERMINAL_PROMPT")
            .Because("a runner that prompts hangs until its lease expires.");
    }

    // ---- the branch a person can trace ----

    [Test]
    public async Task The_naming_rule_is_declared_once_where_both_sides_read_it()
    {
        // The control plane names the branch in the admission and the runner
        // pushes it. A runner deriving the name itself would agree until one side
        // changed, and then a flight would be unable to find the branch it had
        // just created - so the rule lives in the contract, and this asserts the
        // runner holds no copy of it.
        var runnerCopies = RunnerSources()
            .Where(f => CodeOf(f).Contains("\"gg/\"", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(runnerCopies).IsEmpty()
            .Because("the prefix is the contract's. Found: " + string.Join(", ", runnerCopies));

        await Assert.That(typeof(DestinationBranch).Assembly)
            .IsEqualTo(typeof(FactKinds).Assembly)
            .Because("it crosses, so it is declared where things that cross are declared.");
    }

    [Test]
    public async Task The_branch_name_carries_the_flight_number()
    {
        // GG-42 is the thing a person can type and the thing that ties a branch
        // back to a record. A name nobody can trace is a branch nobody will
        // ever delete.
        await Assert.That(DestinationBranch.For("GG-42")).IsEqualTo("gg/GG-42");
        await Assert.That(DestinationBranch.IsOurs("gg/GG-42")).IsTrue();
        await Assert.That(DestinationBranch.IsOurs("main")).IsFalse();
    }

    [Test]
    public async Task A_flight_number_that_would_not_be_a_ref_name_is_made_into_one()
    {
        // The number is GG-42 shaped and this removes nothing in a healthy
        // system. It is here because the value is passed to git, and git has
        // opinions about what is in a ref name.
        await Assert.That(DestinationBranch.For("GG-42 ../../etc")).DoesNotContain("..");
        await Assert.That(DestinationBranch.For("GG-42 ../../etc")).DoesNotContain(" ");
    }

    // ---- the credential is the capability, and only the credential ----

    [Test]
    public async Task Write_scope_is_read_from_what_the_developer_registered()
    {
        // Declared once and read by both sides, because two derivations of "can
        // this write" is how one side comes to believe a flight may land when it
        // may not.
        await Assert.That(CredentialScopes.AllowWrite([CredentialScopes.Read])).IsFalse();
        await Assert.That(CredentialScopes.AllowWrite([CredentialScopes.Write])).IsTrue();
        await Assert.That(CredentialScopes.AllowWrite([CredentialScopes.Read, CredentialScopes.Write]))
            .IsTrue();
        await Assert.That(CredentialScopes.AllowWrite([])).IsFalse()
            .Because("no scopes is not write scope, and defaulting would be the generosity this "
                   + "whole design exists to refuse.");
    }

    [Test]
    public async Task Nothing_in_the_runner_derives_write_permission_from_an_envelope()
    {
        // The version of the mistake that will be proposed: "the envelope says
        // there is a destination, so treat the credential as able to write".
        // That would make the developer's own store advisory.
        var offenders = RunnerSources()
            .Where(f => CodeOf(f).Contains("Scopes = ", StringComparison.Ordinal)
                     || CodeOf(f).Contains("scopes.Add", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the runner READS what the developer registered and never composes a scope "
                   + "set of its own. Found: " + string.Join(", ", offenders));
    }

    // ---- no destination, no write ----

    [Test]
    public async Task A_runner_nobody_configured_to_write_has_nothing_to_write_with()
    {
        // NO DESTINATION, NO WRITE - held at the level of which objects exist
        // rather than by a check somebody could delete. A flight that declared
        // nothing reaches a runner with no adapter to reach, so it is provably
        // unable to push rather than declining to.
        var adapters = DestinationConfiguration.FromEnvironment(
            _ => throw new InvalidOperationException("nothing should be constructed"),
            apis: "", hosts: "forge=forge.example.com");

        await Assert.That(adapters).IsEmpty()
            .Because("reading is configured and writing is not, which is the ordinary state.");
    }

    [Test]
    public async Task Landing_needs_a_second_declaration_beyond_the_one_that_allows_reading()
    {
        // Write is deployment knowledge somebody has to type twice: the git host
        // takes the branch and the api is asked for the proposal. A runner
        // configured to read is not thereby configured to write.
        var adapters = DestinationConfiguration.FromEnvironment(
            api => new HttpClient { BaseAddress = new Uri(api) },
            apis: "forge=https://api.forge.example/",
            hosts: "forge=forge.example.com");

        await Assert.That(adapters.Single().Provider).IsEqualTo("forge");
    }

    [Test]
    public async Task A_destination_for_a_provider_this_runner_cannot_reach_is_refused_by_name()
    {
        // Article XI on a variable. Skipped quietly, this fails on one flight
        // much later, for a reason nothing connects back to a typo.
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            DestinationConfiguration.FromEnvironment(
                _ => new HttpClient(), apis: "other=https://api.other.example/",
                hosts: "forge=forge.example.com"));

        await Assert.That(thrown!.Message).Contains("other");
        await Assert.That(thrown.Message).Contains(VcsConfiguration.HostsVariable);
    }

    // ---- absent admission is refusal ----

    [Test]
    public async Task An_absent_admission_is_a_refusal_rather_than_a_default()
    {
        // Null means do not push, for every reason at once: no destination, an
        // unmet obligation, or a control plane too old to answer. A runner that
        // treated absence as anything else would land work on the strength of a
        // field it could not see.
        var accepted = new FactBatchAccepted { Accepted = 3, Duplicates = 0, Rejected = [] };

        await Assert.That(accepted.Admission).IsNull();
    }

    [Test]
    public async Task The_runner_reads_admission_from_the_response_and_not_from_the_facts_it_sent()
    {
        // Article IX at its narrowest: the runner CAN see the facts it produced
        // and could compute an obligation itself. A runner that did would be
        // deciding, and a patched one would decide differently - so the decision
        // travels as a decision rather than as the inputs to one.
        var loop = RunnerSources().Single(f => Path.GetFileName(f) == "RunnerLoop.cs");
        var source = File.ReadAllText(loop);

        await Assert.That(source).Contains("Admission")
            .Because("it acts on the decision the control plane sent.");

        foreach (var deciding in (string[])["ObligationEngine", "Satisfied", "Violated", "IsAtOrBelow"])
        {
            await Assert.That(source).DoesNotContain(deciding)
                .Because($"'{deciding}' in the loop would be the runner working out for itself "
                       + "whether it is allowed to land.");
        }
    }
}
