using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// An agent's login ceremony runs over the channel: <c>begin-agent-login</c>
/// brings back the URL a person visits, <c>finish-agent-login</c> carries the
/// code they were given, and the token never crosses in either direction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two verbs, because the ceremony has two halves a person stands between.</b>
/// The runner drives its agent's own <c>setup-token</c>; the person visits the
/// URL it prints and comes back with a code; the runner types the code and
/// keeps what the agent mints. The URL is safe to show - PKCE, measured in the
/// spike: the code is useless without the verifier the child holds - and the
/// token is written under the agent's locator and said back as
/// <c>ConfiguredCredential</c> is: locator and whether, never the value.
/// </para>
/// <para>
/// <b>Channel-only, like <c>ConfigureCredentialAsk</c>.</b> No endpoint names
/// any of the four types, asserted rather than intended, so there is no
/// request body a code or a token could enter on its way to the control plane.
/// </para>
/// </remarks>
public class AnAgentCanBeLoggedInOverTheChannelTests
{
    private const string Escape = "";

    [Test]
    public async Task Both_kinds_are_registered_four_ways()
    {
        // THE FOUR REGISTRATIONS RunnerAskVocabularyTests HOLDS, for the values
        // that are new: the vocabulary, the pin, the member declaration, and
        // the slot on the envelope.
        foreach (var (kind, payload, slot) in new[]
        {
            (RunnerAskKinds.BeginAgentLogin, typeof(BeginAgentLoginAsk), "BeginAgentLogin"),
            (RunnerAskKinds.FinishAgentLogin, typeof(FinishAgentLoginAsk), "FinishAgentLogin"),
        })
        {
            await Assert.That(RunnerAskKinds.All).Contains(kind);
            await Assert.That(payload.GetCustomAttribute<PinnedIdAttribute>()).IsNotNull();
            await Assert.That(payload.GetCustomAttribute<RunnerAskKindAttribute>()?.Kind)
                .IsEqualTo(kind);
            await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(payload)).IsTrue();
            await Assert.That(typeof(RunnerAsk).GetProperty(slot)).IsNotNull()
                .Because("the kind says which slot is filled, and a kind with no slot cannot "
                       + "be constructed.");
        }

        foreach (var (answer, slot) in new[]
        {
            (typeof(AgentLoginBegun), "LoginBegun"),
            (typeof(AgentLoginFinished), "LoginFinished"),
        })
        {
            await Assert.That(answer.GetCustomAttribute<PinnedIdAttribute>()).IsNotNull();
            await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(answer)).IsTrue();
            await Assert.That(typeof(RunnerSaid).GetProperty(slot)).IsNotNull();
        }

        await Assert.That(RunnerAskKinds.All.Count).IsEqualTo(5)
            .Because("three verbs became five, and a sixth is a decision made here.");
    }

    [Test]
    public async Task The_code_is_bounded_by_the_contract()
    {
        // A CODE IS SHORT. The bound is the contract's so that both ends refuse
        // the same thing; a runner that trusted the length it was sent would be
        // a runner a console could type a file into its agent.
        var bound = typeof(RunnerAskBounds).GetField(nameof(RunnerAskBounds.MaxLoginCode))!
            .GetRawConstantValue();
        await Assert.That(bound).IsEqualTo(512);
    }

    [Test]
    public async Task No_endpoint_can_carry_the_login_asks_or_answers()
    {
        foreach (var endpoint in ProtocolSurface.Endpoints)
        {
            foreach (var carried in (Type?[])
                [endpoint.Request, endpoint.Response, endpoint.PendingResponse])
            {
                foreach (var channelOnly in (Type[])
                    [typeof(BeginAgentLoginAsk), typeof(FinishAgentLoginAsk),
                     typeof(AgentLoginBegun), typeof(AgentLoginFinished)])
                {
                    await Assert.That(Reaches(carried, channelOnly)).IsFalse()
                        .Because($"{endpoint.Method} {endpoint.Path} could carry a login code or "
                               + "a login URL to the control plane, which is Article VIII by a "
                               + "different route.");
                }
            }
        }
    }

    [Test]
    public async Task Nothing_comes_back_that_could_hold_the_token()
    {
        // BOTH ANSWERS, because the token is minted at the END of the ceremony
        // and a begun-answer that grew a member for it later would pass the
        // finished-answer's check.
        foreach (var answer in (Type[])[typeof(AgentLoginBegun), typeof(AgentLoginFinished)])
        {
            var members = answer.GetProperties();

            foreach (var member in members)
            {
                foreach (var word in (string[])
                    ["secret", "token", "password", "passphrase", "bearer", "apikey", "code"])
                {
                    await Assert.That(member.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                        .IsFalse()
                        .Because($"'{answer.Name}.{member.Name}' is named for secret material on "
                               + "the way BACK - and the code is the person's to carry in, never "
                               + "the runner's to echo.");
                }

                await Assert.That(member.PropertyType == typeof(string)
                               || member.PropertyType == typeof(bool)
                               || member.PropertyType == typeof(DateTimeOffset)
                               || member.PropertyType == typeof(DateTimeOffset?))
                    .IsTrue()
                    .Because($"'{answer.Name}.{member.Name}' is a {member.PropertyType.Name} - a "
                           + "free-form container carries a secret while passing a name check.");
            }

            await Assert.That(members.Length).IsLessThanOrEqualTo(5)
                .Because("the member set is closed, so a sixth member is a decision somebody "
                       + "makes here rather than one that arrives.");
        }
    }

    [Test]
    public async Task What_comes_back_is_stripped_like_everything_else()
    {
        // THE URL IS A LINE OFF A HOSTILE MACHINE, and it is about to be printed
        // and handed to a browser. An escape sequence in it is a forged second
        // line on the person's terminal; a line break in it is a second URL.
        var said = new RunnerSaid
        {
            Kind = RunnerAskKinds.BeginAgentLogin,
            LoginBegun = new AgentLoginBegun
            {
                Provider = "claude",
                Started = true,
                Url = $"https://example.test/authorize?x=1{Escape}[2J\nhttps://evil.test/",
                Diagnosis = $"{Escape}]0;title{Escape}\\fine\nsecond line",
                ExpiresAt = DateTimeOffset.UnixEpoch,
            },
            LoginFinished = new AgentLoginFinished
            {
                Provider = "claude",
                Locator = $"local:agent/claude{Escape}[31m",
                Written = true,
                Diagnosis = $"{Escape}[1mbold",
            },
        }.Stripped();

        await Assert.That(said.LoginBegun!.Url).DoesNotContain(Escape);
        await Assert.That(said.LoginBegun.Url).DoesNotContain("\n")
            .Because("a URL that can contain a newline is a URL that can hide a second one.");
        await Assert.That(said.LoginBegun.Url).Contains("https://example.test/authorize?x=1");
        await Assert.That(said.LoginBegun.Diagnosis).DoesNotContain(Escape);
        await Assert.That(said.LoginBegun.Diagnosis).Contains("fine");
        await Assert.That(said.LoginBegun.ExpiresAt).IsEqualTo(DateTimeOffset.UnixEpoch)
            .Because("a moment is not text, and stripping must leave it alone.");
        await Assert.That(said.LoginFinished!.Locator).DoesNotContain(Escape);
        await Assert.That(said.LoginFinished.Locator).Contains("local:agent/claude");
        await Assert.That(said.LoginFinished.Diagnosis).DoesNotContain(Escape);
        await Assert.That(said.LoginFinished.Written).IsTrue();
    }

    [Test]
    public async Task The_finished_answer_has_the_configured_answers_shape()
    {
        // THE SAME ACT, SAID THE SAME WAY. A credential landed under a locator,
        // and whether: what `configure-credential` says back, plus which agent
        // and why not. A reader of one reads the other.
        foreach (var name in (string[])["Locator", "Written"])
        {
            await Assert.That(typeof(AgentLoginFinished).GetProperty(name)).IsNotNull()
                .Because($"ConfiguredCredential has {name}, and the finished login is the same "
                       + "fact about the same file.");
        }
    }

    private static bool Reaches(Type? from, Type wanted, HashSet<Type>? seen = null)
    {
        if (from is null || from == wanted)
        {
            return from is not null;
        }

        seen ??= [];

        if (!seen.Add(from) || from.Namespace?.StartsWith("Gg.Contracts", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return from.GetProperties().Any(p =>
            Reaches(p.PropertyType, wanted, seen)
            || (p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericArguments().Any(a => Reaches(a, wanted, seen))));
    }
}
