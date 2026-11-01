# Preparing a standalone Store release

The standalone repository is [evg-b/store](https://github.com/evg-b/store). Keep the contents of this package directory at the repository root, so `package.json` is available directly. Keep `Runtime`, `Tests`, `Samples~`, `Documentation~`, both READMEs, license, and existing Unity `.meta` files.

## Before uploading

1. Read the [validation report](validation.md). Complete the Unity Editor checks that are still marked as unverified.
2. Check that the Counter Store sample compiles after import into a clean project with record support configured in its own assembly.
3. Run the package's Editor tests and the sample's documented Button scenario.
4. Keep both READMEs pointed at `https://github.com/evg-b/store.git` and use only tags or commit hashes that exist in that repository.
5. Review the package version. Create release tags only after choosing and validating the release version.
6. Check local Markdown links, license, sample paths, package metadata, and preserved GUIDs in the extracted repository.

Do not carry over project assets, generated `Library` data, or application-specific integrations. The runtime assembly must keep `noEngineReferences: true` and remain free of application dependencies.

## Verify the uploaded result

Clone [evg-b/store](https://github.com/evg-b/store) into a new directory, install its root `package.json` in a clean Unity project, enable record support for the consumer's assembly, and repeat the sample and Editor tests.

Once a real tag or full commit hash exists, document that exact Git revision in both READMEs. Root-package Git URLs need no `?path=` suffix. Follow Unity's [Git dependency syntax](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html).

This guide describes release preparation; it is not evidence that a release or Unity scenario has been completed.
