# packaging

One install, one executable, one version.

A gg release ships a single self-contained Native AOT binary per platform:
`gg`. The console and the runner role live in the same binary; `gg runner up`
spawns `gg runner serve` as a separate OS process.

The version's single source of truth is `VersionPrefix` in
`Directory.Build.props`. CI stamps prereleases as `0.1.0-alpha.N` where N is
the commit count on main; the `GlyphGuild.Gg.Contracts` NuGet package uses
the same scheme.

The actual archive/installer tooling is not designed yet. This directory will
hold it when it is.
