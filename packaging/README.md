# packaging

One executable, one version, **one install per shape** — and each shape updates
by a different command, which is the part that used to go unsaid.

## What a release ships

**A .NET tool package**, `GlyphGuild.Gg.Cli`, pushed to **nuget.org** and also
attached to the release. The command it installs is `gg`; the package id
differs only because `gg` belongs to somebody else on nuget.org.

**A self-contained Native AOT binary per platform**, `gg-<rid>.tar.gz`, for
`linux-x64` and `osx-arm64`. These need nothing on the machine — no runtime, no
SDK, no compiler.

Both are built and attached by `.github/workflows/publish-cli.yml`.

## The shapes, and how each one moves forward

| shape | who installs it | updates by |
|---|---|---|
| .NET tool | anything with the SDK, including every pool host | `dotnet tool update -g GlyphGuild.Gg.Cli --version X` |
| native binary | a machine with no SDK | downloading it again |
| container image | pool members | rebuilt and repinned by digest |

**`gg` moves none of those bytes itself.** It does not download, verify,
replace or roll back its own binary — `dotnet` does all four for the tool, and
a person does them for the other two. Every line of that not written here is a
line that cannot be wrong on one of three platforms.

## Why nuget.org, when the release already carried the package

`dotnet tool install --add-source` takes a **directory**, not a URL. Off a
releases page the tool was download-then-install, and `dotnet tool update`
could not see it at all. Since `gg` builds no updater on purpose, the update
path has to exist somewhere — and a feed is the only place it does.

**`--version` is the better default.** `dotnet tool update` with no version
takes whatever is newest, and newest is whatever was pushed last. See below.

## What signing actually gives, measured rather than assumed

This was checked on .NET SDK 10.0.102 rather than reasoned about, because the
design leans on it.

**1. nuget.org repository-signs every package it accepts.** That proves the
package came through nuget.org's pipeline. It does **not** prove glyph-guild
sent it: anyone holding the API key pushes, nuget.org signs, and the signature
is as valid as any other. The signature defends against tampering *after*
acceptance — not against a compromised publishing account, which is the
likelier path.

**2. On Linux and macOS the client does not verify by default.** `dotnet tool
install` prints, in as many words:

```
Skipping NuGet package signature verification.
```

`DOTNET_NUGET_SIGNATURE_VERIFICATION=true` turns it on, and the notice
disappears. Anything that says "nuget.org signs, the client verifies" is
describing Windows.

**3. Verification on is still not "only signed packages install."** With that
variable set, an unsigned package from a local `--add-source` directory
installs without a word. Verifying a signature that is present is a different
thing from requiring one, and requiring a *particular* signer needs trusted
signers configured in `nuget.config`. None of that is set up here.

So the honest summary: **whatever can push is the trust root**, and the
signature is a tamper seal on the pipeline rather than a statement about who
published.

## Which is why nothing here holds a publishing key

**Trusted publishing, not a stored secret.** The `nuget` job presents a
short-lived OIDC token, nuget.org validates it against a policy naming this
repository *and this workflow file*, and returns an API key good for **one hour
and one push**. There is no long-lived credential to leak, rotate, or find in a
log — and a `ToolPublishingTests` case fails the build if one reappears under
any name.

Three things about that job are load-bearing and none of them is obvious:

- **`permissions:` replaces the default set rather than adding to it.** Trimming
  that block reads like hygiene and silently ends publishing, at the one step
  that runs only on `main` after a version bump.
- **`id-token: write` is granted to the job, not to a step**, so every action in
  it can mint the token. All three are pinned to commit SHAs — the only place in
  this repository where that is worth the awkwardness.
- **The exchange is the step immediately before the push.** An hour is
  generous, but a token minted at the top of a job that waits on a release is a
  token that can expire, and the failure lands on `main`.

The account also needs a `NUGET_USER` secret: a nuget.org **profile name**, not
an email address. It is not really a credential — the worst a leak costs is a
username — but it is configuration rather than something to commit.

Author signing remains the only thing that would prove *glyph-guild published
this* rather than *this pipeline did*, and on Linux and macOS it would buy
nothing without `DOTNET_NUGET_SIGNATURE_VERIFICATION` and trusted signers
configured on every host. See the plan's open question 2.

## Asking what is current, without an account

The flat container index answers it anonymously:

```
https://api.nuget.org/v3-flatcontainer/glyphguild.gg.cli/index.json
```

**The id is lowercased in that URL** and the response is a JSON object with a
`versions` array. A package that does not exist yet does **not** answer 404
with JSON — it returns an Azure Blob `BlobNotFound` XML body, which is worth
knowing by anything that reads this: *absent* and *unreachable* have to stay
distinguishable, or silence reads as being up to date.

## The version

The single source of truth is `VersionPrefix` in `Directory.Build.props`, and
every project inherits it — including `Gg.Client`, which is the assembly
`gg --version` actually reads.

**Bumping that number is what causes a release.** The workflow skips when the
tag already exists, because a published version is immutable and re-uploading
its assets would churn the timestamp on files people have pinned. The failure
mode that creates: forgetting to bump means a release that silently does not
happen.

**The push to nuget.org happens last, and that is not arbitrary.** A GitHub
release can be deleted; a version on nuget.org cannot — unlisting hides it from
search while every client that knows the number still resolves it. The
reversible act goes first.

`GlyphGuild.Gg.Contracts` is deliberately **not** on this scheme — it declares
its own `<Version>` and moves only when the wire surface does, so that a
consumer seeing the number change learns something. See the comment at the top
of `Gg.Contracts/Gg.Contracts.csproj`.

## What CI proves about which artefact

`ci.yml` has an `aot` job that publishes the native binary and runs
`gg --version`, and a `tool` job that packs the package, installs it to a
`--tool-path` and runs the command from there. **Both, because they are
different builds of the same code**: the package is IL, packed with
`-p:PublishAot=false`, and once provisioning installs the tool the artefact
most machines run would otherwise be the one nothing checked.

`--tool-path` rather than `-g` is deliberate in that job: a global install is
invoked off `PATH`, and a green step could mean some other `gg` answered.
