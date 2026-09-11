using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// The channel with no feed behind it, and the one thing that makes its bytes
/// checkable.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tool has a second opinion and the tarball has none.</b> A package on
/// a feed is repository-signed by the feed, which proves it came through that
/// pipeline; <c>packaging/README.md</c> measures exactly what that is worth and
/// what it is not. The native binaries have no equivalent at all. The install
/// line a release prints is a download piped straight into <c>tar</c> — the
/// bytes are extracted before anything could have looked at them, and there is
/// nothing to look at them with.
/// </para>
/// <para>
/// <b>An attestation is not a checksum published next to the file.</b> Whoever
/// can replace an asset on a release can replace a sums file on the same page,
/// so a digest hosted beside the bytes it describes proves a download was not
/// truncated and nothing else. A provenance attestation is signed by the forge
/// against the workflow run that produced the subject, and it is held
/// somewhere the release page cannot rewrite — which is why this asks for that
/// and not for a second file.
/// </para>
/// <para>
/// <b>And the proof and the instruction have to ship together.</b> An
/// attestation nobody is told to check is a cost with no reader, and a
/// verification command in the notes with no attestation behind it sends a
/// person to a 404 that reads exactly like tampering. Each half is asserted
/// here so neither can leave alone.
/// </para>
/// </remarks>
public class AReleaseAssetProvesWhereItCameFromTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    /// <summary>
    /// Every workflow in this repository, found by what one IS.
    /// </summary>
    /// <remarks>
    /// By shape rather than by path, for the reason
    /// <c>EveryWorkflowThatBuildsFetchesTheForkTests</c> gives at length: the
    /// directory these live in is named after an identity provider, and
    /// <c>ProviderNeutralityTests</c> forbids one appearing in a source file.
    /// </remarks>
    private static IEnumerable<string> Workflows() =>
        Directory.EnumerateFiles(Root(), "*.yml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("jobs:", StringComparison.Ordinal)
                    && text.Contains("runs-on:", StringComparison.Ordinal);
            });

    private static string WorkflowPath(string name) =>
        Workflows().FirstOrDefault(f => Path.GetFileName(f) == name)
        ?? throw new InvalidOperationException($"no workflow named {name} was found in this repository");

    /// <summary>The name of a job, at the one indent a job name sits at.</summary>
    private static readonly Regex JobHeading = new(@"^  ([A-Za-z0-9_-]+):\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Each job in a workflow, as its own text.
    /// </summary>
    /// <remarks>
    /// Scoped rather than searched whole, for the reason <c>ToolPublishingTests</c>
    /// gives one job over: "the file says it somewhere" is satisfied by a
    /// property held by a job that does not need it while the one that does
    /// goes without.
    /// </remarks>
    private static IEnumerable<(string Name, string Text)> Jobs(string workflow)
    {
        var lines = File.ReadAllLines(workflow);
        var headings = lines
            .Select((line, at) => (match: JobHeading.Match(line), at))
            .Where(x => x.match.Success)
            .Select(x => (name: x.match.Groups[1].Value, x.at))
            .ToList();

        for (var i = 0; i < headings.Count; i++)
        {
            var from = headings[i].at + 1;
            var to = i + 1 < headings.Count ? headings[i + 1].at : lines.Length;

            yield return (headings[i].name, string.Join('\n', lines[from..to]));
        }
    }

    /// <summary>The job that attaches the assets a person downloads.</summary>
    private static (string Name, string Text) TheJobThatPublishes()
    {
        var release = Jobs(WorkflowPath("publish-cli.yml"))
            .Where(j => j.Text.Contains("release create", StringComparison.Ordinal))
            .ToList();

        return release.Count == 1
            ? release[0]
            : throw new InvalidOperationException(
                $"publish-cli.yml has {release.Count} jobs that create a release, and this asks "
                + "about the one that does");
    }

    [Test]
    public async Task Everything_a_release_attaches_says_which_run_produced_it()
    {
        // THE ASSET IS THE ONLY ARTEFACT WITH NO OTHER WITNESS. The package is
        // on a feed that signs what it accepts and answers questions about it
        // later; the tarball exists in exactly one place, uploaded by one step,
        // and after that its bytes are whatever the page serves.
        var (name, job) = TheJobThatPublishes();

        await Assert.That(job).Contains("attest-build-provenance")
            .Because($"the '{name}' job publishes the binaries people install and nothing signs "
                   + "them. A release asset with no attestation cannot be told apart from one "
                   + "somebody replaced, by anybody, ever.");

        await Assert.That(job).Contains("attestations: write")
            .Because("a permissions block REPLACES the default set rather than adding to it, so "
                   + "the attestation step fails at run time on main without this - which is the "
                   + "one place a failure costs a release.");
    }

    [Test]
    public async Task What_is_attested_is_what_is_uploaded()
    {
        // THE DRIFT THIS EXISTS FOR. Attesting some of what a release carries
        // is worse than attesting none of it: `gh attestation verify` answers
        // for the file it was handed, so an asset nobody attested fails
        // verification exactly as a tampered one does, and the person checking
        // learns to stop checking.
        var (_, job) = TheJobThatPublishes();

        var uploaded = Regex.Match(job, @"release create[^\n]*?\s(\S+/\S+)\s");
        var subject = Regex.Match(job, @"subject-path:\s*'?([^'\n]+)'?");

        await Assert.That(uploaded.Success).IsTrue()
            .Because("this reads the upload glob out of the release step so the two cannot drift; "
                   + "if that step no longer names one, this test is asking the wrong question.");
        await Assert.That(subject.Success).IsTrue()
            .Because("an attestation step with no subject attests nothing and still goes green.");

        var uploadedFrom = uploaded.Groups[1].Value.Split('/')[0];
        var attestedFrom = subject.Groups[1].Value.Trim().Split('/')[0];

        await Assert.That(attestedFrom).IsEqualTo(uploadedFrom)
            .Because($"the release uploads everything under '{uploadedFrom}' and the attestation "
                   + $"covers '{attestedFrom}'. Anything in the first and not the second ships "
                   + "unprovable.");
    }

    [Test]
    public async Task A_job_that_can_mint_a_token_pins_every_action_it_runs()
    {
        // THE RULE THE NUGET JOB ALREADY FOLLOWS, HELD ACROSS THE FILE RATHER
        // THAN AT ONE JOB. `id-token: write` is granted to a JOB, so every
        // action running in it can mint the forge's OIDC token - not only the
        // step that is supposed to. A movable tag on any of them means whoever
        // controls that tag chooses what runs while that credential is
        // available for the asking.
        //
        // ToolPublishingTests holds the publishing job to this. It stops being
        // enough the moment a second job needs a token, which is what signing
        // for provenance requires - so the rule is asked of whichever jobs have
        // the permission, found rather than named.
        var offenders = new List<string>();
        var holders = 0;

        foreach (var workflow in Workflows())
        {
            foreach (var (name, text) in Jobs(workflow))
            {
                if (!text.Contains("id-token: write", StringComparison.Ordinal))
                {
                    continue;
                }

                holders++;

                offenders.AddRange(text
                    .Split('\n')
                    .Where(l => l.Contains("uses:", StringComparison.Ordinal))
                    .Where(l => !Regex.IsMatch(l, @"@[0-9a-f]{40}\b"))
                    .Select(l => $"{Path.GetFileName(workflow)} [{name}]: {l.Trim()}"));
            }
        }

        await Assert.That(holders).IsGreaterThan(0)
            .Because("no job in this repository can mint a token, so this guard proves nothing "
                   + "and would keep proving it after one could.");

        await Assert.That(offenders).IsEmpty()
            .Because("an action in a token-holding job is code that can trade that token for the "
                   + "right to publish gg or to sign for it. Found: " + string.Join(" | ", offenders));
    }

    [Test]
    public async Task The_notes_a_release_carries_say_how_to_check_it()
    {
        // BOTH HALVES OR NEITHER. The attestation is worth what it is checked
        // by, and the only place a person downloading a tarball reads anything
        // is the release page that offered it. The command belongs beside the
        // install line it qualifies, not in a document somebody would have to
        // know to look for.
        var (_, job) = TheJobThatPublishes();

        await Assert.That(job).Contains("attestation verify")
            .Because("the notes print an install line that pipes a download into tar. A person "
                   + "who is never told the bytes can be checked will not check them, and the "
                   + "attestation this workflow produces has no reader.");
    }
}
