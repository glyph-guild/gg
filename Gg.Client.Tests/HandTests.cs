using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The hard direction: a person hands back, and nobody writes a summary.
/// </summary>
/// <remarks>
/// <para>
/// Step 6 worked because the agent had written its reasoning down. <b>Nothing
/// writes the human's</b>, and the answer is not to ask - a design that asks for
/// a summary has already failed, because a person who has just worked for two
/// hours writes "fixed it".
/// </para>
/// <para>
/// So the agent reads the diff and proposes what appears to have been done, and
/// the person corrects it. <b>The confirmation is a trust boundary, not a
/// politeness:</b> an agent's proposal about a human's work is a guess, and a
/// confirmed account is a human assertion attributed under Article XII. The
/// confirmation is the step that converts one into the other.
/// </para>
/// </remarks>
public class HandTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private const string Esc = "\u001b";

    private static TakeMeasurements Measured() => new()
    {
        FilesEdited = ["src/orders.py", "src/config.py"],
        FilesReadNotEdited = ["ISSUE.md", "config/settings.yaml"],
        Searches = [],
        Errors = [],
        RefusedMoves = [],
        Attempts = 11,
        StopReason = LoopOutcomes.Completed,
        Verdict = "violated",
    };

    private static ProposedAccount AProposal(string text = "You moved rounding into total().") =>
        new() { Proposal = text };

    // ---- nothing is recorded until a person confirms ----

    [Test]
    public async Task Walking_away_records_nothing_and_the_flight_says_so()
    {
        // THE BOUNDARY. Recording an unconfirmed proposal as somebody's account
        // is putting words in their mouth and signing their name to them.
        var outcome = HandConfirmation.Confirm(
            "principal", AProposal(), new HandChoice.WalkedAway(), T0);

        await Assert.That(outcome.Account).IsNull()
            .Because("an account is a person's own statement, so it is not written for them.");
        await Assert.That(outcome.Detail).Contains("no account from you")
            .Because("the absence is the record. A flight with no account from the person who "
                   + "worked on it is a true statement; a guess shown as theirs is a false one.");
    }

    [Test]
    public async Task An_empty_edit_is_the_walk_away_case_wearing_a_different_button()
    {
        // Somebody who opened the box and left. Recording an empty statement
        // would be recording that they said nothing, as though they had.
        foreach (var choice in (HandChoice[])[new HandChoice.Edit("   "), new HandChoice.Replace("")])
        {
            var outcome = HandConfirmation.Confirm("principal", AProposal(), choice, T0);

            await Assert.That(outcome.Account).IsNull();
            await Assert.That(outcome.Choice).IsEqualTo("walked-away");
        }
    }

    [Test]
    public async Task Accepting_with_nothing_proposed_records_nothing()
    {
        // The inference failed and somebody pressed accept. There is nothing to
        // accept, and an empty statement recorded as an assertion is worse than
        // no assertion.
        var outcome = HandConfirmation.Confirm(
            "principal", new ProposedAccount { Proposal = "", Absence = "the agent could not be run" },
            new HandChoice.Accept(), T0);

        await Assert.That(outcome.Account).IsNull();
        await Assert.That(outcome.Detail).Contains("no proposal to accept");
    }

    // ---- what a confirmation produces ----

    [Test]
    public async Task An_accepted_proposal_becomes_the_persons_own_assertion()
    {
        var outcome = HandConfirmation.Confirm(
            "principal", AProposal(), new HandChoice.Accept(), T0);

        await Assert.That(outcome.Account!.By).IsEqualTo("principal")
            .Because("attributed to the person, not to the agent that drafted it.");
        await Assert.That(outcome.Account.Confirmation).IsEqualTo(AccountConfirmations.Accepted);
        await Assert.That(outcome.Account.WasProposed).IsTrue()
            .Because("an accepted account is one somebody read and agreed with, and that is worth "
                   + "a reader knowing.");
        await Assert.That(outcome.Account.Statement).IsEqualTo("You moved rounding into total().");
    }

    [Test]
    public async Task An_edited_proposal_records_what_they_wrote_rather_than_what_was_proposed()
    {
        var outcome = HandConfirmation.Confirm(
            "principal", AProposal(),
            new HandChoice.Edit("I moved rounding into total() and added a test for 0.70 + 0.10."),
            T0);

        await Assert.That(outcome.Account!.Statement).Contains("0.70");
        await Assert.That(outcome.Account.Statement).DoesNotContain("You moved");
        await Assert.That(outcome.Account.Confirmation).IsEqualTo(AccountConfirmations.Edited);
    }

    [Test]
    public async Task A_replaced_proposal_is_still_recorded_as_having_had_one()
    {
        // The escape, and the signal. It is a real account either way, and the
        // fact that a proposal was discarded is the number that says whether the
        // premise held.
        var outcome = HandConfirmation.Confirm(
            "principal", AProposal(), new HandChoice.Replace("None of that. I reverted it."), T0);

        await Assert.That(outcome.Account!.Confirmation).IsEqualTo(AccountConfirmations.Replaced);
        await Assert.That(outcome.Account.WasProposed).IsTrue();
        await Assert.That(outcome.Choice).IsEqualTo(AccountConfirmations.Replaced);
    }

    [Test]
    public async Task The_account_is_stripped_and_bounded_like_any_inline_item()
    {
        var outcome = HandConfirmation.Confirm(
            "principal", AProposal(),
            new HandChoice.Replace($"{Esc}]0;pwnedI fixed {Esc}[31mit{Esc}[0m"), T0);

        await Assert.That(outcome.Account!.Statement).DoesNotContain(Esc);
        await Assert.That(outcome.Account.Statement).IsEqualTo("I fixed it");

        var enormous = HandConfirmation.Confirm(
            "principal", AProposal(),
            new HandChoice.Replace(new string('x', HumanAccount.MaxStatement + 500)), T0);

        await Assert.That(enormous.Account!.Statement.Length).IsEqualTo(HumanAccount.MaxStatement);
    }

    // ---- what the agent is asked ----

    [Test]
    public async Task Nobody_is_asked_to_write_a_summary()
    {
        // The design premise, asserted on the thing that would break it. The
        // prompt asks the AGENT to describe what the person did; nothing asks
        // the person for prose before they have something to correct.
        var prompt = HandPrompt.Compose("GG-12", "I could not round safely.", Measured());

        await Assert.That(prompt).Contains("Read `git diff`");
        await Assert.That(prompt).Contains("APPEAR TO HAVE DONE");

        foreach (var asking in (string[])["Please summarise", "write a summary", "describe your work"])
        {
            await Assert.That(prompt).DoesNotContain(asking);
        }
    }

    [Test]
    public async Task The_prompt_asks_the_agent_to_connect_their_work_to_its_own_concerns()
    {
        // THE PART THAT EARNS THIS FEATURE. A proposal built from the diff alone
        // describes a diff, which is what git is for.
        var prompt = HandPrompt.Compose(
            "GG-12",
            "No rounding applied. A rate like 0.2 can yield 180.00000000000003.",
            Measured());

        await Assert.That(prompt).Contains("180.00000000000003")
            .Because("its own prior account goes in, or there is nothing to connect to.");
        await Assert.That(prompt).Contains("SAY SO EXPLICITLY");
        await Assert.That(prompt).Contains("Do not invent a connection")
            .Because("a proposal that manufactures a link is worse than one that describes a diff.");
    }

    [Test]
    public async Task With_no_prior_account_the_agent_is_told_not_to_speculate()
    {
        var prompt = HandPrompt.Compose("GG-12", priorAccount: null, Measured());

        await Assert.That(prompt).Contains("left no account");
        await Assert.That(prompt).Contains("without speculating");
    }

    // ---- the inference is bounded, and cannot write ----

    [Test]
    public async Task The_inference_is_read_only_and_wall_clock_bounded()
    {
        // Not a loop - a loop discharges an obligation and summarising
        // discharges nothing - but outside the envelope must not mean unbounded.
        // An agent reading a customer's repository with no limits is the thing
        // this whole design exists to prevent.
        await Assert.That(HandBounds.Moves).IsEquivalentTo((string[])[LoopMoves.Read, LoopMoves.Search]);
        await Assert.That(HandBounds.Moves).DoesNotContain(LoopMoves.Edit);
        await Assert.That(HandBounds.Moves).DoesNotContain(LoopMoves.RunTests);
        await Assert.That(HandBounds.WallClock).IsEqualTo(TimeSpan.FromMinutes(2))
            .Because("it reads a diff and a paragraph, and a person is standing at the terminal.");
    }

    [Test]
    public async Task Nothing_in_the_hand_path_can_ask_for_a_writing_move()
    {
        // Structural, because the bounds are hardcoded and a hardcoded bound is
        // one somebody edits without thinking about it.
        var offenders = Sources()
            .Where(f => Path.GetFileName(f) is "HandProposal.cs" or "HandConfirmation.cs")
            .Where(f => Code(f).Contains("LoopMoves.Edit", StringComparison.Ordinal)
                     || Code(f).Contains("LoopMoves.RunTests", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the inference reads. Found: " + string.Join(", ", offenders));

        // Liveness: the scan is looking at files that really do name moves.
        await Assert.That(Sources().Any(f => Path.GetFileName(f) == "HandProposal.cs"
                                          && Code(f).Contains("LoopMoves.Read", StringComparison.Ordinal)))
            .IsTrue();
    }

    [Test]
    public async Task The_hardcoded_bounds_are_recorded_as_a_gap_with_a_trigger()
    {
        // Hardcoded is honest for one fixed task with one fixed shape. What
        // makes it honest is saying when it stops being so.
        var source = File.ReadAllText(
            Sources().Single(f => Path.GetFileName(f) == "HandProposal.cs"));

        await Assert.That(source).Contains("second utility invocation")
            .Because("a gap with no trigger is a gap nobody closes.");
    }

    // ---- the diff does not cross ----

    [Test]
    public async Task The_diff_never_reaches_the_account_that_crosses()
    {
        // ABSENCE, TWIN, LIVENESS. The inference runs where the tree is and only
        // the confirmed account travels - so a diff in the account would be
        // source content crossing a boundary that exists to stop it.
        const string needle = "-    return sum(i[\"price\"] for i in items)";

        // The twin: the needle really is in what the agent was given to read.
        var prompt = HandPrompt.Compose("GG-12", "I left rounding alone.", Measured());

        await Assert.That(prompt).Contains("git diff")
            .Because("the agent is pointed at the diff, which is why its absence downstream means "
                   + "something.");

        // A person accepts a proposal that quoted the diff at them. What is
        // recorded is what they confirmed - and nothing here reaches out for the
        // diff itself.
        var outcome = HandConfirmation.Confirm(
            "principal", AProposal("You changed the sum in total()."), new HandChoice.Accept(), T0);

        await Assert.That(outcome.Account!.Statement).DoesNotContain(needle);

        // And structurally: nothing in this path reads a tree or runs git.
        foreach (var file in Sources().Where(f =>
                     Path.GetFileName(f) is "HandProposal.cs" or "HandConfirmation.cs"))
        {
            // REACHING for it, not mentioning it. The prompt tells the agent to
            // read `git diff` and must - that is the whole point, and a scan
            // that could not tell an instruction from an invocation would be
            // deleted by the next person rather than obeyed.
            foreach (var reaching in (string[])["File.", "Directory.", "Process"])
            {
                await Assert.That(new Regex($@"\b{Regex.Escape(reaching)}").IsMatch(Code(file)))
                    .IsFalse()
                    .Because($"{Path.GetFileName(file)} reaching for '{reaching}' would be the diff "
                           + "arriving somewhere it does not belong.");
            }
        }
    }

    [Test]
    public async Task The_human_account_is_not_a_field_of_the_digest()
    {
        // Same rule as the agent's, and one more reason: a digest is a machine
        // record computed identically every run, and this is a person's
        // statement, which is different every time by construction.
        var digest = typeof(LoopDigest).GetProperties().Select(p => p.Name).ToList();

        foreach (var prose in (string[])["Human", "Account", "Statement", "Confirmation"])
        {
            await Assert.That(digest).DoesNotContain(prose)
                .Because($"'{prose}' on the digest would make two runs of the same work compare "
                       + "unequal, and comparison across flights is the whole of the hardening.");
        }

        await Assert.That(digest).Contains(nameof(LoopDigest.FilesReadNotEdited));
    }

    // ---- the round trip, which is the acceptance test ----

    [Test]
    public async Task Hand_then_take_again_carries_the_first_takers_account_marked_as_theirs()
    {
        // THE ACCEPTANCE TEST. Nothing resumes the loop, so the value of a
        // hand-back is only real if the record reaches the next reader - and the
        // one consumer that already exists is the next takeover.
        var confirmed = HandConfirmation.Confirm(
            "alice",
            AProposal(),
            new HandChoice.Edit(
                "I moved rounding into total() at the boundary, which addresses the float issue "
              + "the agent flagged. I did not switch to Decimal - that is a product decision."),
            T0).Account;

        var seed = TakeSeedComposer.Compose(
            "GG-12", "019ff8cc-1111-7000-8000-00000000000c", "/work/tree",
            new LoopDigest
            {
                LoopId = "implement",
                FilesReadNotEdited = ["config/settings.yaml"],
                FilesEdited = ["src/orders.py"],
                Searches = [],
                Errors = [],
                RefusedMoves = [],
                Attempts = 11,
                StopReason = LoopOutcomes.Completed,
            },
            account: "No rounding applied.",
            priorHuman: confirmed);

        var rendered = TakeSeedComposer.Render(seed);

        await Assert.That(rendered).Contains("A PERSON WORKED ON THIS BEFORE YOU")
            .Because("the second taker finds it where a resuming reader looks.");
        await Assert.That(rendered).Contains("alice")
            .Because("attributed. An assertion nobody can be attributed to is not one.");
        await Assert.That(rendered).Contains("a human assertion")
            .Because("marked as a person's claim, distinct from the agent's account above it and "
                   + "from the measurements above that.");
        await Assert.That(rendered).Contains("edited the account proposed to them");
        await Assert.That(rendered).Contains("Decimal");

        // And all three kinds are present and distinguishable in one document.
        var measuredAt = rendered.IndexOf("MEASURED", StringComparison.Ordinal);
        var agentAt = rendered.IndexOf("THE AGENT'S OWN ACCOUNT", StringComparison.Ordinal);
        var humanAt = rendered.IndexOf("A PERSON WORKED ON THIS", StringComparison.Ordinal);

        await Assert.That(measuredAt).IsLessThan(agentAt);
        await Assert.That(agentAt).IsLessThan(humanAt)
            .Because("ours, then the agent's, then the person's - weakest claim to strongest, "
                   + "which is the order somebody reads them in.");
    }

    [Test]
    public async Task A_seed_with_no_prior_human_account_does_not_pretend_there_is_one()
    {
        var rendered = TakeSeedComposer.Render(TakeSeedComposer.Compose(
            "GG-12", "flight-1", "/work/tree", digest: null, account: null));

        await Assert.That(rendered).DoesNotContain("A PERSON WORKED ON THIS");
    }

    private static string Code(string file) =>
        string.Join('\n', File.ReadAllLines(file)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)));

    private static IEnumerable<string> Sources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory
            .EnumerateFiles(Path.Combine(root.FullName, "Gg.Client"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
