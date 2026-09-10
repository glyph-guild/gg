using System.Text.Json;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// The tracker sink, against a real tracker.
/// </summary>
/// <remarks>
/// <para>
/// <b>Category RealRemote, and excluded from CI by name</b> — it needs a real
/// project and a real credential with work-item write scope, and it CREATES,
/// re-fields, scores and links real work items. They are tagged so a person can
/// find and delete them; nothing here deletes anything, because a suite that
/// cleans up is a suite that can hide what it did.
/// </para>
/// <para>
/// <b>Why this exists at all.</b> Every other assertion about this sink is over
/// a stub handler, and <c>RealPathScopedAdapterTests</c>' own remark says what
/// that arrangement hid for months: "a test that fakes the credential is asking
/// whether the shape compiles, not whether the service accepts it". This is a
/// WRITE adapter against somebody's backlog, so the gap between those two
/// questions is the whole risk. Slice thirty-four's lesson stated plainly: four
/// defects stood between the code being right and the feature working, and only
/// a live walk found any of them.
/// </para>
/// <para>
/// <b>What can only be learned here.</b> Whether this shape accepts a
/// json-patch on the routes used; whether a created item comes back with an id
/// in the field this reads; whether `System.Tags` accepts and returns the
/// idempotency tag, and whether WIQL's `CONTAINS` finds it — the retry story
/// rests entirely on that last one and a stub cannot say anything about it;
/// whether `Microsoft.VSTS.Common.Priority` takes an arbitrary string, which is
/// the one place the contract's refusal to type a score meets a tracker that
/// may well type it; and whether a relation added twice is refused or
/// duplicated.
/// </para>
/// </remarks>
[Category("RealRemote")]
public class RealTrackerSinkTests
{
    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{variable} is not set. This suite needs a real project and a real credential "
              + "with work-item write scope, and it creates real work items; it is excluded "
              + "from CI by category for that reason.");

    /// <summary>
    /// The tracker root, from configuration, because naming one here is the
    /// violation this repository's neutrality guard exists to catch.
    /// </summary>
    private static string Host =>
        $"{Required("GG_ADO_HOST")}/{Required("GG_ADO_ORG")}/{Required("GG_ADO_PROJECT")}";

    private static WiqlWorkItemSink Sink() =>
        new(Host, Required("GG_ADO_SECRET"), new HttpClient());

    private static IWorkItemSource Reader() =>
        new WiqlWorkItemSource(Host, Required("GG_ADO_SECRET"), new HttpClient());

    /// <summary>
    /// A field this project types as an integer, so the contract's decision to
    /// send every value as a string is actually tested rather than assumed.
    /// </summary>
    /// <remarks>
    /// Named from configuration for the reason the host is: this binary must
    /// not know a customer's schema. Defaulted to nothing - a walk that
    /// silently skipped the typed case would leave the open question open
    /// while looking answered.
    /// </remarks>
    private static string IntegerField => Required("GG_ADO_INTEGER_FIELD");

    /// <summary>A key nothing else will ever use, so the retry search is honest.</summary>
    private static string AKey() => "walk-" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// What a created item should be, in this project's process.
    /// </summary>
    /// <remarks>
    /// <b>Named rather than defaulted, because the default would succeed and be
    /// wrong.</b> The adapter falls back to <c>Issue</c> when a proposal's
    /// detail does not say, and <c>Issue</c> exists in the Agile process
    /// alongside <c>User Story</c> - so a walk that let the fallback run would
    /// file Issues onto a Stories backlog, pass every assertion here, and leave
    /// somebody a column of the wrong thing. Which type a project has is a fact
    /// about a deployment, so it arrives the way the host does.
    /// </remarks>
    private static string ItemType =>
        Environment.GetEnvironmentVariable("GG_ADO_ITEM_TYPE") is { Length: > 0 } declared
            ? declared
            : throw new InvalidOperationException(
                "GG_ADO_ITEM_TYPE is not set. This walk CREATES work items, and the type is "
              + "the one thing it must not guess: `Issue` exists in the Agile process beside "
              + "`User Story`, so guessing succeeds and files the wrong kind onto somebody's "
              + "backlog. Set it to what this project's process calls a story - `User Story` "
              + "on Agile, `Product Backlog Item` on Scrum, `Issue` on Basic.");

    private static JsonElement Detail(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private static WorkItemProposal Creating(string title) => new()
    {
        Operation = WorkItemOperations.Create,
        Reason = "Created by gg's slice-thirty-five walk. Safe to delete.",
        Detail = Detail(
            $$"""{"type":"{{ItemType}}","title":"{{title}}","description":"gg walk, safe to delete"}"""),
    };

    [Test]
    public async Task A_create_lands_and_comes_back_with_an_id_this_can_read()
    {
        var key = AKey();

        var written = await Sink().PerformAsync([Creating("gg walk " + key)], key);

        await Assert.That(written.Count).IsEqualTo(1);
        await Assert.That(written[0].AlreadyDone).IsFalse();
        await Assert.That(written[0].Target).IsNotEmpty()
            .Because("a create that reports no id leaves an item nothing can find again, "
                   + "including the retry this flight would make.");

        // THE READER IS THE WITNESS. Asserting the write's own report would be
        // asserting what this code decided; reading it back asks the tracker.
        var read = await Reader().ReadAsync(written[0].Target);

        await Assert.That(read).IsNotNull()
            .Because($"item {written[0].Target} was just created and the reader cannot find it.");
        await Assert.That(read!.Title).Contains(key, StringComparison.Ordinal);
    }

    [Test]
    public async Task A_second_run_with_the_same_key_finds_the_first_items_work()
    {
        // THE ASSERTION A STUB CANNOT MAKE. The retry story rests on WIQL's
        // `CONTAINS` finding the tag this sink wrote, and whether a tag written
        // through json-patch is queryable that way - and how soon - is a fact
        // about the tracker.
        var key = AKey();
        var sink = Sink();

        var first = await sink.PerformAsync([Creating("gg walk " + key)], key);
        var second = await sink.PerformAsync([Creating("gg walk " + key)], key);

        await Assert.That(second[0].AlreadyDone).IsTrue()
            .Because("the second run must find the first run's item. If this fails, the "
                   + "idempotency mechanism does not work against this tracker and a lost "
                   + "report duplicates a work item - which is the failure the seam's rule "
                   + $"exists to prevent. First: {first[0].Target}, second: {second[0].Target}");

        await Assert.That(second[0].Target).IsEqualTo(first[0].Target);
    }

    [Test]
    public async Task A_score_is_taken_as_the_string_the_rubric_wrote()
    {
        // WHERE THE CONTRACT'S REFUSAL TO TYPE A SCORE MEETS A TRACKER THAT
        // MAY. The contract keeps it a string on purpose; if this field will
        // only take an integer, that is a fact the adapter has to carry rather
        // than one the contract should have decided.
        var key = AKey();
        var created = await Sink().PerformAsync([Creating("gg walk " + key)], key);

        var scored = await Sink().PerformAsync(
            [new WorkItemProposal
            {
                Operation = WorkItemOperations.Score,
                Target = created[0].Target,
                Score = "2",
                Reason = "walked",
            }],
            AKey());

        await Assert.That(scored[0].AlreadyDone).IsFalse();
    }

    [Test]
    public async Task A_custom_field_takes_a_string_even_when_the_tracker_types_it()
    {
        // S36.0-03, AND THE ANSWER WAS NOT OBVIOUS. The contract sends every
        // field value as a string, because what a field holds belongs to the
        // rubric and typing it here would decide for every tracker at once.
        // This project's scoring fields are declared INTEGER, so that decision
        // only survives if the tracker coerces - and it does: `"8"` into an
        // integer field reads back as 8.
        //
        // Measured rather than assumed, because the same walk already found
        // that relation urls name the project by GUID. This shape's
        // conventions are not guessable.
        var key = AKey();
        var created = await Sink().PerformAsync([Creating("gg walk " + key)], key);

        var scored = await Sink().PerformAsync(
            [new WorkItemProposal
            {
                Operation = WorkItemOperations.Field,
                Target = created[0].Target,
                Reason = "the rubric scores this an eight",
                Fields = [new WorkItemFieldEdit { Path = IntegerField, Value = "8" }],
            }],
            AKey());

        await Assert.That(scored[0].AlreadyDone).IsFalse();

        var read = await Reader().ReadAsync(created[0].Target);
        await Assert.That(read).IsNotNull()
            .Because("asserting the write's own report would assert what this code decided.");
    }

    [Test]
    public async Task A_value_the_field_will_not_take_names_the_field()
    {
        // THE OTHER HALF, and the reason the diagnosis carries the tracker's
        // own sentence. A patch is one request for every field in it, so one
        // bad value refuses the lot - and "nothing was written" without saying
        // WHICH field sends somebody to read every field definition.
        var key = AKey();
        var created = await Sink().PerformAsync([Creating("gg walk " + key)], key);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Sink().PerformAsync(
                [new WorkItemProposal
                {
                    Operation = WorkItemOperations.Field,
                    Target = created[0].Target,
                    Reason = "a rubric that produced a word where a number goes",
                    Fields = [new WorkItemFieldEdit { Path = IntegerField, Value = "high" }],
                }],
                AKey()));

        await Assert.That(refused!.Message).Contains(IntegerField, StringComparison.Ordinal)
            .Because("the tracker said `Invalid field status 'InvalidType' for field "
                   + $"'{IntegerField}'`, and dropping that on the floor is the expensive "
                   + $"half. Said: {refused.Message}");
    }

    [Test]
    public async Task A_link_added_twice_is_added_once()
    {
        var a = await Sink().PerformAsync([Creating("gg walk a " + AKey())], AKey());
        var b = await Sink().PerformAsync([Creating("gg walk b " + AKey())], AKey());

        WorkItemProposal Linking() => new()
        {
            Operation = WorkItemOperations.Link,
            Target = a[0].Target,
            Reason = "walked",
            Detail = Detail($$"""{"to":"{{b[0].Target}}"}"""),
        };

        var first = await Sink().PerformAsync([Linking()], AKey());
        var again = await Sink().PerformAsync([Linking()], AKey());

        await Assert.That(first[0].AlreadyDone).IsFalse();
        await Assert.That(again[0].AlreadyDone).IsTrue()
            .Because("this shape APPENDS relations, so a retry that did not read first would "
                   + $"leave {a[0].Target} related to {b[0].Target} twice.");
    }

    [Test]
    public async Task A_credential_without_write_scope_refuses_at_the_write_and_says_so()
    {
        // THE CASE THE CONSTRUCTOR CANNOT CATCH. A token that is present and
        // read-only passes construction and fails at the write, with a 401 or a
        // 403 - and this records which, because "the token was wrong" and "the
        // token cannot do this" send a person to different places.
        if (Environment.GetEnvironmentVariable("GG_ADO_READONLY_SECRET") is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "GG_ADO_READONLY_SECRET is not set. This asks what a read-only token does at "
              + "a WRITE, which is the failure the constructor's refusal cannot catch and the "
              + "one an operator is most likely to hit. Set it to a token with work-item read "
              + "and no write.");
        }

        var readOnly = new WiqlWorkItemSink(
            Host, Environment.GetEnvironmentVariable("GG_ADO_READONLY_SECRET"), new HttpClient());

        var refused = await Assert.ThrowsAsync<HttpRequestException>(
            async () => await readOnly.PerformAsync([Creating("gg walk refused")], AKey()));

        await Assert.That(refused!.StatusCode)
            .IsEqualTo(System.Net.HttpStatusCode.Unauthorized)
            .Or.IsEqualTo(System.Net.HttpStatusCode.Forbidden)
            .Because("whichever it is, it is now written down: " + refused.Message);
    }
}
