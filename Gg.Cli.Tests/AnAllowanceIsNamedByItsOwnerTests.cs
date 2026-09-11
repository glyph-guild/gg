using Gg.Cli;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// How a machine says which allowance it spends from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A name, and never the thing itself.</b> This file's own rule —
/// <i>"it names which credential to use and never the credential"</i> — applies
/// unchanged to a subscription. What crosses to a control plane is the string a
/// person typed here, not an account id, an email, an organisation uuid or a
/// token, and nothing in <c>Gg.Local</c> can resolve one.
/// </para>
/// <para>
/// <b>Two runners may carry the same name, and that is the feature.</b> A
/// laptop and a server signed in to one subscription are one allowance; saying
/// so is how their spending is added up rather than counted twice.
/// </para>
/// <para>
/// <b>The ceilings are here because nowhere else has them.</b> No file on the
/// machine records a subscription's limits, so the denominator is typed by the
/// person who knows which plan they are on.
/// </para>
/// </remarks>
public class AnAllowanceIsNamedByItsOwnerTests
{
    [Test]
    public async Task The_file_carries_the_name_and_the_ceilings()
    {
        foreach (var key in new[] { "allowance", "allowance-limits" })
        {
            var member = Configuration.Members.SingleOrDefault(m => m.Key == key);

            await Assert.That(member).IsNotNull()
                .Because($"a member without a row here is one the file silently cannot "
                       + $"carry, and nothing holds it to the blank rule. '{key}' is "
                       + "missing.");
        }

        var written = Configuration.Members
            .Single(m => m.Key == "allowance")
            .With(new Configuration(), "kdee-max");

        await Assert.That(written.Allowance).IsEqualTo("kdee-max");
        await Assert.That(Configuration.Members.Single(m => m.Key == "allowance").Get(written))
            .IsEqualTo("kdee-max")
            .Because("an accessor pair that disagreed would set one member and read "
                   + "another, and a whole-document round trip would not notice.");

        var ceilings = Configuration.Members
            .Single(m => m.Key == "allowance-limits")
            .With(new Configuration(), "session=88000,week=2400000");

        await Assert.That(ceilings.AllowanceLimits).IsEqualTo("session=88000,week=2400000");
    }

    [Test]
    public async Task A_blank_allowance_is_refused_and_named()
    {
        var refused = Configuration.Validate(new Configuration { Allowance = "   " });

        await Assert.That(refused).IsNotNull()
            .Because("somebody who typed an empty allowance wrote a value; reading it as "
                   + "unset means the line did nothing and nothing said so.");
        await Assert.That(refused!).Contains("allowance", StringComparison.OrdinalIgnoreCase)
            .Because($"a refusal that does not name the line cannot be acted on. "
                   + $"Said: {refused}");
    }

    [Test]
    public async Task Both_are_on_the_environment_page()
    {
        var named = ConsoleEnvironment.Read().Select(s => s.Name).ToList();

        await Assert.That(named).Contains("GG_ALLOWANCE");
        await Assert.That(named).Contains("GG_ALLOWANCE_LIMITS")
            .Because("the page exists to say what this binary reads. A ceiling read in "
                   + "production and named on no surface is the half a person needs when "
                   + "the percentage on their screen looks wrong.");
    }
}
