using Gg.Contracts.Authoring;
using static Gg.Contracts.Tests.McpFixtures;

namespace Gg.Contracts.Tests;

/// <summary>
/// S72.1-02: a document carries a credential's locator and never its value
/// (ADR-0040 Decision 3).
/// </summary>
/// <remarks>
/// The refusal of a literal secret can only be a heuristic: a setting whose name
/// says it holds one must carry a reference. That catches the common mistake, a
/// token pasted where its locator belongs, and is not a promise that nothing
/// secret can be written.
/// </remarks>
public class AnMcpSecretIsAReferenceTests
{
    [Test]
    public async Task A_reference_and_a_plain_value_are_accepted()
    {
        await Assert.That(Envelope.Validate(Root([Hosted()]), Roles.Root)).IsNull();
        await Assert.That(Envelope.Validate(Root([Local()]), Roles.Root)).IsNull();
    }

    [Test]
    public async Task A_literal_authorization_header_is_refused_naming_the_server_and_the_header()
    {
        var refusal = Envelope.Validate(Root([Hosted(authorization: "Bearer squ_abc123")]), Roles.Root);

        await Assert.That(refusal).IsNotNull()
            .Because("the control plane stores locators and never values.");
        await Assert.That(refusal!).Contains("sonarqube");
        await Assert.That(refusal!).Contains("Authorization");
    }

    [Test]
    public async Task A_literal_in_a_variable_named_for_a_secret_is_refused()
    {
        foreach (var name in (string[])["SONARQUBE_TOKEN", "API_KEY", "DB_PASSWORD", "CLIENT_SECRET"])
        {
            var server = Local() with { Env = [new McpSetting { Name = name, Value = "abc123" }] };
            var refusal = Envelope.Validate(Root([server]), Roles.Root);

            await Assert.That(refusal).IsNotNull().Because($"{name} names a secret.");
            await Assert.That(refusal!).Contains(name);
        }
    }

    [Test]
    public async Task A_reference_to_something_that_is_not_a_locator_is_refused()
    {
        var server = Local() with
        {
            Env = [new McpSetting { Name = "SONARQUBE_TOKEN", Value = "${credential:../../etc/passwd}" }],
        };

        await Assert.That(Envelope.Validate(Root([server]), Roles.Root)).IsNotNull();
    }

    [Test]
    public async Task A_reference_in_an_argument_is_refused()
    {
        // Arguments are on the command line, which every user on the host can read.
        var server = Local() with { Args = ["mcp-sonarqube@1.1.1", "--token", "${credential:" + Locator + "}"] };
        var refusal = Envelope.Validate(Root([server]), Roles.Root);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("argument");
    }
}
