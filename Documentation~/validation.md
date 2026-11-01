# Store validation

Date: 2026-11-01.

These results cover the standalone Store package prepared for [evg-b/store](https://github.com/evg-b/store), including its record-based Counter Store sample. They are not a Unity Editor or player certification.

## Results

| Check | Result | What it establishes |
| --- | --- | --- |
| Runtime compilation | Passed, 11 C# source files | The runtime compiles as a separate assembly without Unity Engine references. |
| Existing NUnit tests | 17 passed, 0 failed, 0 skipped | Portable execution of the package's current test source under Mono. |
| English README Quick start | Compiled and ran; output `0`, then `10` | The exact snippet works with the documented consumer-side `IsExternalInit` type. |
| Russian README Quick start | Compiled and ran; output `0`, then `10` | Both README examples are identical and executable. |
| Missing compatibility type | Expected `CS0518` reproduced without the shim | The Mono framework target needs the documented record prerequisite. |
| Complete Counter Store sample | Compiled with a real cached `UnityEngine.CoreModule.dll` reference | C# syntax and referenced API compatibility with that Android build artifact. |
| Sample model and reducer | Passed outside Unity | Record value equality, notifications `0,1,2`, preservation of previous states, irrelevant/zero actions, and subscription disposal. |
| Collection example | Compiled and ran | New collection references, preserved earlier snapshots, and a no-op for duplicate items. |

The runtime source, tests, runtime assembly definition, and package version were not changed by this documentation update. The sample's handwritten state equality was replaced by record equality.

## Evidence and environment

- [NUnit result XML](validation/mono-results.xml)
- [SHA-256 of the checked runtime, test, sample, and example documents](validation/source-sha256.txt)
- C# compiler: Roslyn `csc 3.9.0-6.21124.20`, with `-langversion:9.0`.
- Runtime: Mono `6.12.0.162`.
- Test framework: the project's cached Unity NUnit framework, loaded with `NUnitTestAssemblyRunner` and `TestFilter.Empty`.
- Runtime and test assemblies were built with `-debug:portable -optimize-`; tests ran with `mono --debug`.

The publication candidate was compiled from a separate package copy with `package.json` at its root. Both README C# examples were extracted from that copy and executed as shown above.

The source-location diagnostic test uses debug symbols to verify the subscription's source file and line.

The result XML retains test names, assertions, timestamps, and outcomes. Temporary directory prefixes were removed from attributes, and process/app-domain properties were omitted for portability. No test results were changed.

The full sample was compiled against `UnityEngine.CoreModule.dll` from an existing Android build in the local project's `Library`, plus Mono's `netstandard` facade. No Unity API stubs were used. That assembly is verification input only and is not included in this package.

## Unity checks still required

The project-scoped Unity MCP tools were not available in this session. Unity CLI reported a running project with Pipeline `0.8.0-exp.1`, but its server was unreachable. `editor_status` returned:

```text
No Pipeline instance found for project.
Make sure Unity Editor is running with the Pipeline package installed.
```

No Pipeline upgrade, Editor restart, project import, or Play Mode action was performed. Therefore the following remain **unverified**:

1. Installing this package in a clean Unity project.
2. Adding the compatibility type to the consumer assembly and importing Counter Store through Package Manager.
3. Compiling the imported sample in Unity and checking the Editor Console.
4. Running `Evg.Store.Tests` through the Unity Test Runner.
5. Clicking the sample's Button in Play Mode and observing Console output and component destruction.
6. Building a player, including IL2CPP.

The portable checks and independent source review do not establish these Unity behaviors. Repeat the [sample steps](../Samples~/CounterStore/README.md) and update this report after an Editor run.

## Package and documentation checks

The project package-validation script passed after the documentation/sample changes. It checks package manifests, sample directories, documentation presence, matching dependency versions, and runtime/test C# meta files. A script meta file was added for the sample, which previously had none; existing package GUIDs were preserved.

All 35 relative Markdown links and image references and their anchors were checked, including evidence links. New document meta files and the sample script meta file are present, their GUIDs are unique in the project, and the working-tree diff passes whitespace checks. Both READMEs use the root-package Git URL for [evg-b/store](https://github.com/evg-b/store). Published tags and clean-project Unity installation still belong to the [release preparation](publishing.md) stage.
