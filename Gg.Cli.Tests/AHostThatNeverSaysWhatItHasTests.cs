using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// Every runner this binary starts is handed the reporter that says which
/// environment instances its host has.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured in the field on 2026-10-01, and it made the whole feature
/// inert.</b> vmlinux001 had a provisioned instance — <c>gg-env-1</c> serving
/// <c>ui</c>, its daemon reachable by the runner — and a <c>ui-preview</c> flight
/// declaring <c>hosts: [ui]</c> was never offered to it. The runner logged
/// <c>nothing ready</c> sixteen times and nothing else. No error was raised
/// anywhere, by anything.
/// </para>
/// <para>
/// <b>The cause was one unsupplied argument.</b> <c>RunnerHost.RunAsync</c>
/// declares <c>EnvironmentReporter? environments = null</c>, and no production
/// caller passed one — all three call sites pass <c>machine:</c> and stop. So the
/// host never POSTed <c>/v1/runner/environment/instances</c>,
/// <c>gg_environment_instance</c> stayed empty, and the control plane's pick
/// predicate — which requires a row there matching the environment and the
/// machine — could never match. A flight asking to be hosted is simply stepped
/// over, for ever, in silence.
/// </para>
/// <para>
/// <b><c>UnsuppliedInputTests</c> is the right guard and could not see this
/// one.</b> That scan matches calls by name, and <c>RunAsync</c> is declared on
/// many types here, so the parameter lands in its <c>Undecidable</c> set rather
/// than its findings — which that guard reports rather than drops, deliberately.
/// This is the named follow-up the undecidable case asks for, and it is specific
/// on purpose: a scan that cannot decide is not a scan that should guess.
/// </para>
/// <para>
/// <b>Textual, because the composition root is the thing being asserted.</b>
/// What is wrong when this fails is not a behaviour any harness here can
/// exercise — it is that the one place which knows both halves did not join
/// them. The same shape as the runbook ratchets, and for the same reason.
/// </para>
/// </remarks>
public class AHostThatNeverSaysWhatItHasTests
{
    /// <summary>Every `RunnerHost.RunAsync(` call in the composition root, with its arguments.</summary>
    private static IReadOnlyList<string> Invocations()
    {
        var program = UnsuppliedInputs.Production()
            .Single(f => f.Key.EndsWith(Path.Combine("Gg.Cli", "Program.cs"), StringComparison.Ordinal))
            .Value;

        var calls = new List<string>();

        foreach (Match start in Regex.Matches(program, @"RunnerHost\.RunAsync\("))
        {
            // Balance the parentheses, so the whole argument list is read rather
            // than up to the first nested close - every one of these calls
            // contains lambdas and nested calls.
            var depth = 0;
            var from = start.Index + start.Length - 1;

            for (var i = from; i < program.Length; i++)
            {
                if (program[i] == '(') depth++;
                else if (program[i] == ')' && --depth == 0)
                {
                    calls.Add(program[from..(i + 1)]);
                    break;
                }
            }
        }

        return calls;
    }

    [Test]
    public async Task Every_runner_this_binary_starts_says_which_instances_its_host_has()
    {
        var calls = Invocations();

        await Assert.That(calls).IsNotEmpty()
            .Because("if the composition root stopped calling RunnerHost.RunAsync by this name, "
                   + "this guard is reading nothing and would pass on anything.");

        var silent = calls
            .Where(c => !c.Contains("environments:", StringComparison.Ordinal))
            .ToList();

        await Assert.That(silent).IsEmpty()
            .Because($"{silent.Count} of {calls.Count} runner(s) report no environment instances, "
                   + "so a host with a provisioned slot looks to the control plane exactly like a "
                   + "host with none - and every flight whose kind declares `hosts:` is stepped "
                   + "over in silence, which is what vmlinux001 did sixteen times.");
    }

    [Test]
    public async Task The_reporter_it_is_handed_reads_this_host_rather_than_a_fixture()
    {
        // THE POISON TWIN. Passing `environments: null` satisfies the assertion
        // above textually while reporting exactly nothing, which is the state
        // this test exists to end.
        foreach (var call in Invocations())
        {
            var argument = Regex.Match(call, @"environments:\s*([^,\n]+)").Groups[1].Value.Trim();

            await Assert.That(argument).IsNotEqualTo("null");
            await Assert.That(argument).Contains("EnvironmentReporter", StringComparison.Ordinal)
                .Because("the scan that walks /srv/env is the only thing that knows, and a "
                       + $"reporter built from anything else is a second answer. Got: '{argument}'");
        }
    }
}
