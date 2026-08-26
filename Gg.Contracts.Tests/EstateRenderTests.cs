using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every document class the estate holds can be written down.
/// </summary>
/// <remarks>
/// <para>
/// <b>A role with no renderer is a directory <c>pull</c> leaves empty.</b> The
/// strategy renderer was missing for a whole slice and nothing said so: the
/// suites that cover rendering each name their own type, so a type nobody named
/// was covered by nobody, silently. This is the census that makes the absence
/// loud.
/// </para>
/// <para>
/// <b>Discovered from the role vocabulary rather than listed</b>, so a fifth role
/// fails here the day it is added rather than the day somebody notices their
/// working copy is missing a folder.
/// </para>
/// </remarks>
public class EstateRenderTests
{
    /// <summary>Which document type each role's name carries.</summary>
    /// <remarks>
    /// Root and work kinds carry a whole envelope; a narrowing carries
    /// constraints; a strategy carries a management document. This mapping is
    /// ADR-0014's table, and it is the thing a fifth role has to extend.
    /// </remarks>
    private static Type DocumentFor(string role) => role switch
    {
        Roles.Root or Roles.WorkKind => typeof(Envelope),
        Roles.Narrowing => typeof(EnvelopeNarrowing),
        Roles.Strategy => typeof(EnvironmentStrategy),
        _ => throw new ArgumentOutOfRangeException(
            nameof(role), role,
            "This role carries no known document type. A role that reaches here is one "
          + "somebody added to the vocabulary without deciding what its name holds - and "
          + "pull cannot render a document class nobody has named."),
    };

    [Test]
    public async Task Every_role_has_a_document_type_and_a_renderer_for_it()
    {
        var renders = typeof(EnvelopeText)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, "Render", StringComparison.Ordinal))
            .Select(m => m.GetParameters()[0].ParameterType)
            .ToList();

        foreach (var role in Roles.All)
        {
            var document = DocumentFor(role);

            await Assert.That(renders).Contains(document)
                .Because($"role '{role}' carries a {document.Name} and EnvelopeText cannot "
                       + "write one down, so pull would leave its documents out of the tree "
                       + "without saying so");
        }
    }

    [Test]
    public async Task The_census_would_notice_a_role_nobody_thought_about()
    {
        // LIVENESS. The assertion above walks a vocabulary, and a walk that
        // found nothing would pass. This proves the mapping refuses a role it
        // has not been taught rather than quietly answering for it.
        await Assert.That(() => DocumentFor("environment-pool"))
            .Throws<ArgumentOutOfRangeException>();
    }
}
