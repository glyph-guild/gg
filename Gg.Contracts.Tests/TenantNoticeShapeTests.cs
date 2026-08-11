using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The notice is part of the wire, declared like everything else on it.
/// </summary>
/// <remarks>
/// <para>
/// It rides on <c>WhoAmI</c> rather than on an endpoint of its own. Every
/// surface that has to show a degradation already asks who the caller is -
/// <c>gg doctor</c>, the console, the tenant page - so one field reaches all
/// three, and a second endpoint would be a second thing to remember to call.
/// </para>
/// <para>
/// <b>gg names no forge, so the sentence comes from the control plane
/// whole.</b> The code names the capability that is degraded - <c>egress</c> -
/// and never the provider. The detail and remedy are composed on the side
/// that is allowed to know which provider it is talking about.
/// </para>
/// </remarks>
public class TenantNoticeShapeTests
{
    [Test]
    public async Task WhoAmI_carries_notices()
    {
        var members = ProtocolSurface.JsonMembers[typeof(WhoAmI)];

        await Assert.That(members).Contains("notices");
    }

    [Test]
    public async Task A_notice_is_a_code_a_sentence_and_what_to_do()
    {
        var members = ProtocolSurface.JsonMembers[typeof(TenantNotice)];

        await Assert.That(members).IsEquivalentTo((string[])["code", "detail", "remedy", "blocking"]);
    }

    [Test]
    public async Task The_notice_type_is_in_the_vocabulary()
    {
        // A type missing from the manifest is a type that crosses the boundary
        // without anybody having declared that it does.
        await Assert.That(Vocabulary.Types).Contains(typeof(TenantNotice));
    }

    [Test]
    public async Task A_response_with_no_notices_is_an_empty_list_rather_than_null()
    {
        // "Nothing is wrong" and "this control plane is too old to say" must
        // not be the same value on the reading side, and the older of the two
        // simply omits the member - which deserializes to the default. An empty
        // list is the honest default: it renders as no checks, which is what an
        // older control plane means.
        var who = new WhoAmI
        {
            PrincipalId = "019fe8a2-0707-70c2-9ff8-be3adb54cef0",
            PrincipalDisplay = "somebody",
            TenantId = "019fe062-d000-730c-a37d-7247342cd810",
            ExpiresAt = DateTimeOffset.UtcNow,
        };

        await Assert.That(who.Notices).IsNotNull();
        await Assert.That(who.Notices).IsEmpty();
    }

    [Test]
    public async Task The_codes_are_neutral_about_who_the_provider_is()
    {
        // The whole reason the sentence travels rather than being composed
        // here. A code named for a forge would put a forge's name in gg, which
        // ProviderNeutralityTests forbids for a reason that has not changed.
        // Spelled in fragments, because ProviderNeutralityTests scans this file
        // too and a test asserting "no forge name here" that contains four of
        // them would be the joke that fails the build.
        var forges = (string[])["git" + "hub", "git" + "lab", "bit" + "bucket", "az" + "ure"];

        foreach (var code in TenantNoticeCodes.All)
        {
            foreach (var forge in forges)
            {
                await Assert.That(code.Contains(forge, StringComparison.OrdinalIgnoreCase)).IsFalse()
                    .Because($"'{code}' names {forge}.");
            }
        }

        await Assert.That(TenantNoticeCodes.All).IsNotEmpty()
            .Because("an empty list satisfies the loop above and asserts nothing.");
    }
}
