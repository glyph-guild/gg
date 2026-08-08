# packaging

One install, two executables, one version.

A gg release ships a single archive per platform containing:

| executable  | source        | built with            |
| ----------- | ------------- | --------------------- |
| `gg`        | `console/`    | Node 20 (bundled TBD) |
| `gg-runner` | `Gg.Runner/`  | .NET Native AOT       |

Both report the same version. The single source of truth is `0.1.0`, declared
in `Directory.Build.props` (`VersionPrefix`) and `console/package.json`
(`version`) — bump both together, always to the same value. CI stamps
prereleases as `0.1.0-alpha.N` where N is the commit count on main; the
`GlyphGuild.Gg.Contracts` NuGet package uses the same scheme.

The actual bundling (how the Node console is packaged next to the AOT runner)
is not designed yet. This directory will hold the installer/archive tooling
when it is.
