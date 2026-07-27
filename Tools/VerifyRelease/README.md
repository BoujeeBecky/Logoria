# VerifyRelease

Checks that the shipped `Release` build of Logoria contains none of the
development tooling, and no way to reach the network.

It reads assembly **metadata only**, through `MetadataLoadContext`, so it never
executes plugin code. Anyone can run it against the DLL inside `latest.zip`
without trusting the source tree it came from.

```
dotnet build Logoria.csproj -c Release
dotnet run --project Tools\VerifyRelease -- bin\Release\Logoria.dll
```

Expected output ends with `RESULT: clean`. Exit code is 0 on pass, 1 on fail.

## What it asserts

**These types are absent.** They are the capture and probe tooling, removed from
the compile entirely in Release by a `<Compile Remove>` in `Logoria.csproj`:

* `DiagnosticsService`, `DiagnosticsWindow`
* `CallbackCaptureService`, `CapturedCallback`
* `EurekaStateProbe`
* `CapturedEvent`, `ArrayCandidate`

**These assemblies are not referenced:** `System.Net.Http`,
`System.Net.Sockets`, `System.Net.Primitives`. A .NET assembly cannot open a
socket without referencing something that can, so the absence of these is
stronger evidence than reading the source and finding no `HttpClient`.

## Sanity check the check

A verifier that always prints "clean" proves nothing. Point it at the Debug
build, which deliberately *does* contain the tooling:

```
dotnet run --project Tools\VerifyRelease -- bin\Debug\Logoria.dll
```

That must print `RESULT: FAILED` and list all seven types. If both builds pass,
the verifier is broken, not the plugin.
