using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// A tenant is named, and a person can read which one they are in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The asymmetry this closes.</b> `whoami' has printed a principal's
/// display beside its id since the first version of that record, and printed
/// the tenant as a bare GUID. So a person read their own name and then an
/// identifier for the thing that owns all of their work - and on a machine
/// with access to more than one tenant there was no way to confirm which one
/// they were about to open a flight in.
/// </para>
/// <para>
/// <b>The name was never missing, only unread.</b> `identity.gg_tenant.name'
/// is `text NOT NULL' and has been written by the sign-up path since the day
/// it existed. This is the same shape as the walk's defects: the control plane
/// knew and no surface asked.
/// </para>
/// <para>
/// <b>Absent is a real answer.</b> An older control plane sends no name, and
/// the id alone is what a reader prints then - never an empty pair of
/// brackets, which would read as a tenant called nothing.
/// </para>
/// </remarks>
public class ATenantHasANameTests
{
    private static WhoAmI Who(string? tenantDisplay) => new()
    {
        PrincipalId = "01a062f3-42a5-73a4-8c01-ec248bfe5237",
        PrincipalDisplay = "Kevin Deenanauth",
        TenantId = "01a062f3-42a5-73a4-8bf5-29a4bbb36533",
        TenantDisplay = tenantDisplay,
        ExpiresAt = new DateTimeOffset(2026, 9, 21, 7, 16, 38, TimeSpan.Zero),
    };

    private static string Text(WhoAmI who) => VerbOutput.ToText(new VerbResult.Identity(who));

    [Test]
    public async Task Whoami_says_what_the_tenant_is_called()
    {
        await Assert.That(Text(Who("JDXpert")))
            .Contains("Tenant:     JDXpert (01a062f3-42a5-73a4-8bf5-29a4bbb36533)")
            .Because("the display is what a person recognises and the id is what they can "
                   + "take to a support conversation - the pair the principal line above it "
                   + "has always printed.");
    }

    [Test]
    public async Task A_control_plane_that_does_not_say_leaves_the_id_alone()
    {
        var text = Text(Who(tenantDisplay: null));

        await Assert.That(text).Contains("Tenant:     01a062f3-42a5-73a4-8bf5-29a4bbb36533");
        await Assert.That(text).DoesNotContain("()")
            .Because("an older control plane sends no name, and empty brackets would read as "
                   + "a tenant called nothing rather than one nobody has been told about.");
    }

    [Test]
    public async Task A_name_with_a_control_sequence_in_it_is_stripped()
    {
        // A TENANT NAMES ITSELF, so this string is somebody else's text on its
        // way to a terminal - the rule every externally-sourced value in this
        // console is held to.
        await Assert.That(Text(Who("JDX[31mpert"))).DoesNotContain("")
            .Because("a name is chosen by whoever signed the tenant up.");
    }
}
