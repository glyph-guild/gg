using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// Ready may report named values, and what it names is carried whole.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner's decision:</b> <i>"it may additionally report named values, one of
/// which is an address. A web stack reports url=…; a queue consumer reports
/// nothing, or reports queue=orders depth=0. gg stamps preview.url when a url is
/// among them and doesn't when it isn't."</i>
/// </para>
/// <para>
/// <b>Which settles Decision 8 without making every environment a web
/// server.</b> <c>ui-preview.yaml</c> sets <c>PREVIEW_PORT: "8080"</c> and says
/// that number <i>"is not a suggestion and not yours to choose"</i>, while the
/// Angular app binds 4200 and traefik holds 443 and 8080 — a declared port and
/// an orchestrator that assigns them cannot both be right. A reported address
/// makes the declared port a rule nobody needs.
/// </para>
/// <para>
/// <b>An unknown key is KEPT, not dropped.</b> Only <c>url</c> is interpreted,
/// because only it is stamped. The rest is shown to a person — and a reader who
/// has to debug a queue consumer wants <c>depth=0</c> in front of them rather
/// than discarded by a parser that did not recognise it.
/// </para>
/// </remarks>
public class ReadyReportsNamedValuesTests
{
    private static StackScript.ReadyReport Read(string stdout) =>
        StackScript.ReadReady(
            new StackScript.Performance(
                StackOutcomes.Exited, 0, TimeSpan.FromSeconds(1), Survived: false),
            stdout);

    [Test]
    public async Task A_web_stack_reports_its_address()
    {
        var report = Read("ready=yes\nurl=http://127.0.0.1:18080\n");

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Yes);
        await Assert.That(report.Values["url"]).IsEqualTo("http://127.0.0.1:18080");
    }

    [Test]
    public async Task A_queue_consumer_reports_no_address_and_is_still_ready()
    {
        // A WORKING ENVIRONMENT WITH NO ADDRESS, which `PREVIEW_PORT` could not
        // express: it made every environment a thing with a port.
        var report = Read("ready=yes\nqueue=orders\ndepth=0\n");

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Yes);
        await Assert.That(report.Values.ContainsKey("url")).IsFalse();

        await Assert.That(report.Values["queue"]).IsEqualTo("orders");
        await Assert.That(report.Values["depth"]).IsEqualTo("0")
            .Because("an unknown key is kept rather than dropped - a reader debugging a "
                   + "queue consumer wants depth in front of them.");
    }

    [Test]
    public async Task Ready_is_not_itself_a_reported_value()
    {
        // IT IS THE ANSWER, not one of the values. Carrying it in both places
        // would give two sources for one fact, and the wrong one is the easier
        // to reach.
        var report = Read("ready=yes\nurl=http://x\n");

        await Assert.That(report.Values.ContainsKey("ready")).IsFalse();
        await Assert.That(report.Values.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_value_carrying_an_equals_sign_is_kept_whole()
    {
        // SPLIT ON THE FIRST SEPARATOR ONLY. A connection string and a query
        // string both carry `=`, and splitting on every one of them would hand
        // a person half an address.
        var report = Read("ready=yes\nurl=http://x/?a=1&b=2\n");

        await Assert.That(report.Values["url"]).IsEqualTo("http://x/?a=1&b=2");
    }

    [Test]
    public async Task Noise_around_the_values_is_ignored_rather_than_refused()
    {
        // A HOOK IS A SCRIPT AND SCRIPTS PRINT. Compose prints "Creating
        // network", docker prints pull progress - and a parser that refused a
        // line it did not understand would make every working hook look broken.
        // Only lines that look like a report are read.
        var report = Read(
            "Creating network gg_default\n"
          + "ready=yes\n"
          + " url = http://127.0.0.1:18080 \n"
          + "done.\n");

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Yes);
        await Assert.That(report.Values["url"]).IsEqualTo("http://127.0.0.1:18080")
            .Because("a key and a value are trimmed, because a script writing `url = x` means "
                   + "the same thing as one writing `url=x`.");
    }

    [Test]
    public async Task Nothing_reported_is_an_empty_set_rather_than_null()
    {
        var report = Read("ready=yes\n");

        await Assert.That(report.Values).IsNotNull()
            .Because("a caller reading Values has nothing to guard, and an environment that "
                   + "reported no values said something true rather than nothing at all.");

        await Assert.That(report.Values.Count).IsEqualTo(0);
    }
}
