using System.Text;
using System.Text.Json;
using Gg.Contracts;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// What a manifest actually costs, against a budget nobody had measured.
/// </summary>
/// <remarks>
/// The 16 KiB digest budget was proposed with the note "validate against a real
/// gate render before treating these as settled". This is the first real data,
/// and it is a test rather than a note in a report so the number moves when the
/// shape does.
/// </remarks>
public class ManifestSizeTests
{
    private static int Bytes(ChangeManifest manifest) =>
        Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(
            new FactEnvelope
            {
                IdempotencyKey = new string('k', 80),
                Kind = FactKinds.ChangeManifest,
                Digest = new string('0', 64),
                ObservedAt = DateTimeOffset.UnixEpoch,
                Change = manifest,
            },
            JsonSerializerOptions.Web));

    private static ChangeManifest Of(int files)
    {
        var paths = Enumerable.Range(0, files).Select(i => new ChangedPath
        {
            // A path of realistic length. Short names would flatter the number
            // and the number is the point.
            Path = $"src/Gg.ControlPlane.Scheduling/Receptors/Handler{i}.cs",
            Change = ChangeKinds.Modified,
            LinesAdded = 42,
            LinesRemoved = 7,
            Classification = Classifications.Internal,
        }).ToList();

        return new ChangeManifest
        {
            BaseCommit = new string('a', 40),
            HeadCommit = new string('b', 40),
            Resolution = ChangeResolution.Files,
        DiffBasis = Gg.Contracts.DiffBasis.TwoPoint,
            Paths = paths,
            Directories = [],
            Languages = [new LanguageChange
            {
                Language = "csharp", Files = files, LinesAdded = 42 * files, LinesRemoved = 7 * files,
            }],
            FilesChanged = files,
            LinesAdded = 42 * files,
            LinesRemoved = 7 * files,
            PathsWithheld = 0,
        };
    }

    [Test]
    public async Task A_pull_request_of_ordinary_size_fits_with_room_to_spare()
    {
        // Ten files is what most pull requests are, and the budget should not
        // be something an ordinary change has to think about.
        await Assert.That(Bytes(Of(10))).IsLessThan(FactBudget.MaxItemBytes / 4);
    }

    [Test]
    public async Task An_ordinary_pull_request_is_nowhere_near_the_budget()
    {
        // What 16 KiB got wrong. At about 150 bytes a file it ran out around a
        // hundred files, so an ordinary change nearly filled it and the
        // per-directory rollup became the COMMON case - which breaks the claim
        // the hardening rests on, because thirty rollups cannot be compared
        // with each other at all.
        //
        // The gate budgets are a different question with a different answer:
        // 8 KiB an item is what a person reads while deciding something. A
        // digest is machine-comparable history and nobody reads a hundred.
        await Assert.That(Bytes(Of(100))).IsLessThan(FactBudget.MaxItemBytes / 10);
    }

    [Test]
    public async Task The_budget_still_runs_out_somewhere_real()
    {
        // The twin, and the reason the budget is not simply enormous. A rollup
        // that nothing could ever trigger would be untested code guarding a
        // case that never happens, and the day it fired it would be the first
        // time anybody had seen it.
        await Assert.That(Bytes(Of(FactBudget.ManifestFilesWithinBudget * 2)))
            .IsGreaterThan(FactBudget.MaxItemBytes);
    }

    [Test]
    public async Task The_budget_is_derived_from_the_measurement_rather_than_chosen()
    {
        // The per-file cost is recorded in the contract so the budget can be
        // re-derived instead of re-guessed. If a manifest ever gets
        // meaningfully fatter per file, this fails and the number gets
        // revisited - which is the only thing that stops it becoming folklore.
        var measured = (Bytes(Of(200)) - Bytes(Of(100))) / 100;

        await Assert.That(measured).IsLessThan(FactBudget.ManifestBytesPerFile * 2);
        await Assert.That(measured).IsGreaterThan(FactBudget.ManifestBytesPerFile / 2);
    }

    [Test]
    public async Task The_cost_per_file_is_worth_writing_down()
    {
        // A number a person can reason about, rather than one they have to
        // derive from two assertions.
        var perFile = (Bytes(Of(200)) - Bytes(Of(100))) / 100;

        await Assert.That(perFile).IsGreaterThan(80);
        await Assert.That(perFile).IsLessThan(220)
            .Because("about 150 bytes a file, so 16 KiB is roughly a hundred paths.");
    }
}
