namespace Gg.Contracts.Tests;

/// <summary>
/// The move that lets a flight read a repository's history - its commits, what
/// each changed, and who last touched a line - without being able to change
/// anything (slice seventy-four).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a move, and not prose.</b> GG-1081 (ado#18492, 2026-10-11) was told
/// to read the commits that closed the story its ticket named, and every
/// <c>git</c> call it made came back "This command requires approval": a
/// bounded loop launches with the permission check on and an allow-list built
/// from its moves, and no move grants Bash. Measured on Claude Code 2.1: a
/// <c>Bash(git log:*)</c> rule admits only a command typed at the repository's
/// own root, and a flight's root is the tree above its repositories, so
/// <c>git -C</c>, <c>--git-dir</c> and every <c>cd</c> form are refused. The
/// door has to be a tool this platform serves, named whole, the way the three
/// <c>propose-*</c> moves are.
/// </para>
/// <para>
/// <b>Record-only, and it costs a version.</b> Reading history produces
/// nothing a destination gates and reaches nothing outside the tree, so it is
/// record-only like every move before it. And on <c>Write</c>'s reasoning,
/// adding a value to a closed vocabulary is not additive: a reader pinned
/// before it halts on the word, which is the property being bought.
/// </para>
/// </remarks>
public class AMoveReadsTheRepositorysHistoryTests
{
    [Test]
    public async Task History_is_a_move()
    {
        await Assert.That(LoopMoves.History).IsEqualTo("history");
        await Assert.That(LoopMoves.All).Contains(LoopMoves.History)
            .Because("a move not in All is one no envelope may declare and no guard counts.");
    }

    [Test]
    public async Task It_is_record_only()
    {
        await Assert.That(MoveKinds.Of(LoopMoves.History)).IsEqualTo(MoveKinds.RecordOnly)
            .Because("reading a repository's past produces nothing a destination gates and "
                   + "reaches nothing outside the tree. Classifying it is the decision "
                   + "MoveKinds.Of refuses to let anybody skip.");
    }

    [Test]
    public async Task A_loop_may_declare_it_beside_read()
    {
        var envelope = new Envelope
        {
            Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
            Obligations =
            [
                new Obligation
                {
                    Id = "in-scope",
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                },
            ],
            Loops =
            [
                new Loop
                {
                    Id = "investigate",
                    Executor = ExecutorRungs.Frontier,
                    Discharges = ["in-scope"],
                    Moves = [LoopMoves.Read, LoopMoves.Search, LoopMoves.History],
                    Budget = new LoopBudget { WallClock = "20m" },
                    OnExhaustion = ExhaustionPolicies.HandoffToHuman,
                },
            ],
            Destinations =
            [
                new Destination
                {
                    Id = "none",
                    Kind = DestinationKinds.None,
                    Requires = ["in-scope"],
                },
            ],
        };

        await Assert.That(Envelope.Validate(envelope)).IsNull()
            .Because("an investigate loop that reads, searches and reads history is the "
                   + "shape this move exists for.");
    }
}
