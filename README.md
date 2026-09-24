# PKBank

Save Editor for Linux/Android inspired by PKHeX and the 3DS Pokebank app.

## Releases

Releases are built by [`.github/workflows/release.yml`](.github/workflows/release.yml) and shipped as a
single Linux AppImage.

Cutting one:

1. Set `<Version>` in `Directory.Build.props` to the new `x.y.z` and commit it.
2. Tag that commit with the same `x.y.z` (no `v` prefix) and push the tag.

The workflow refuses a tag that does not match `Directory.Build.props`, builds
`PKBank-x.y.z-x86_64.AppImage` and attaches it to a GitHub release for the tag.

To build the AppImage locally:

```sh
packaging/appimage/build-appimage.sh [x.y.z]
```

### Updates

A build started from the AppImage checks the latest GitHub release on launch and offers to replace itself
with a newer one; *Options ▸ Check for Updates…* does the same on demand. Anything started another way (a
`dotnet run` from this checkout) never checks on its own.
