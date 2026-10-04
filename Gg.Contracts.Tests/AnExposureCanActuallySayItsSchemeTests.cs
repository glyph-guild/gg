using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A document can carry the scheme, which is what makes the member more than a type.
/// </summary>
/// <remarks>
/// <para>
/// <b>The member arrived and no document could say it.</b> 0.275.0 added
/// <see cref="ExposureInventory.Scheme"/>, taught the connector to dial https and gave the
/// ingress its <c>noTLSVerify</c> — and the exposure parser's key list stayed
/// <c>size, hostnames, credentials, port</c>. That list is CLOSED, so a document writing
/// <c>scheme: https</c> was refused by the only thing that reads it, and every test passed
/// because each one built the record directly.
/// </para>
/// <para>
/// <b>A field is not a feature until the document can carry it.</b> This is the gap that
/// shape of change leaves: the type, the writer and the consumer all agreed, and the one
/// place a tenant actually writes was never widened.
/// </para>
/// </remarks>
public class AnExposureCanActuallySayItsSchemeTests
{
    private const string WithScheme = """
        kind: cloudflare-tunnel
        inventory:
          size: 8
          hostnames: "jdapp-{slot}.goodgrief.dev"
          credentials: "local:exposure/jdapp-{slot}"
          port: 8080
          scheme: https
        """;

    private const string WithoutScheme = """
        kind: cloudflare-tunnel
        inventory:
          size: 8
          hostnames: "jdapp-{slot}.goodgrief.dev"
          credentials: "local:exposure/jdapp-{slot}"
          port: 8080
        """;

    [Test]
    public async Task A_document_may_say_its_scheme_and_it_is_read()
    {
        var parsed = EnvelopeYaml.ParseExposure(WithScheme);

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because("the key list is closed, so a document saying `scheme:` was refused by the "
                   + "only thing a tenant writes for - which made the member unreachable.");

        await Assert.That(parsed.Exposure!.Inventory.Scheme).IsEqualTo(OriginSchemes.Https)
            .Because("accepting the key and dropping the value would be the same silence one "
                   + "step further in.");
    }

    [Test]
    public async Task A_document_that_says_nothing_still_says_nothing()
    {
        var parsed = EnvelopeYaml.ParseExposure(WithoutScheme);

        await Assert.That(parsed.Diagnosis).IsNull();

        await Assert.That(parsed.Exposure!.Inventory.Scheme).IsNull()
            .Because("every exposure in the field is this one, and absent has to stay absent "
                   + "rather than become a default written into the record.");
    }

    [Test]
    public async Task A_scheme_nobody_declared_is_refused_where_its_author_can_fix_it()
    {
        var parsed = EnvelopeYaml.ParseExposure(WithScheme.Replace("https", "ftp"));

        await Assert.That(parsed.Diagnosis).IsNotNull()
            .Because("a connector handed a scheme it cannot dial writes an ingress nothing "
                   + "answers, and the flight that discovers it is a long way from the person "
                   + "who typed it.");
    }
}
