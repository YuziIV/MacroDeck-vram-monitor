# NVIDIA VRAM Monitor

Macro Deck 3 plugin exposing live memory use of a selected NVIDIA GPU as variables for widgets.

## Use

Install the plugin from the Macro Deck Store in the **Macro Deck desktop app**; Macro Deck starts and authorizes it. To set it up:

1. On the computer running Macro Deck, install an NVIDIA driver and check that `nvidia-smi` is available on `PATH`.
2. Open **Integrations → NVIDIA VRAM Monitor** and choose **Select an NVIDIA GPU**. Enter its zero-based GPU index (`0` for the first GPU) and save. You can change it later in the integration settings.
3. Add a **History Graph** widget and select `vram-used-percent` with a 0–100 range, or show one of the other variables below.

| Variable | Meaning |
| --- | --- |
| `vram-used-percent` | Percentage of GPU memory in use |
| `vram-used-mib` | Used memory in MiB |
| `vram-total-mib` | Total memory in MiB |

The plugin runs `nvidia-smi` locally about once a second, querying the configured GPU. If it cannot read the device or `nvidia-smi` is missing, readings are unavailable. It does not send GPU data anywhere or require an account. Supported host platforms are Windows x64 and Linux x64 with an NVIDIA driver; macOS is not supported.

The plugin's icon and documentation were created with AI assistance. The running plugin does not use AI or generate AI content.

To test before publishing, start Macro Deck, create a one-time token under **Developer Tools → Plugin tokens**, save it as `MacroDeck:Plugin:EnrollmentToken` in the **source project's .NET User Secrets**, and run the **Macro Deck - Real Host** debug profile in `src/NvidiaVramMonitor/Properties/launchSettings.json`. Remove the token from User Secrets after pairing; subsequent debug launches reuse the ignored `.macrodeck-dev-state/`. Check GPU 0, change to another GPU if available, and try an invalid index to confirm readings become unavailable. Store installations need no developer enrollment token.

## Build and publish

Requires the .NET 10 SDK and the [Macro Deck plugin CLI](https://docs.macro-deck.app/). The packaged plugin uses the .NET 10 runtime supplied by Macro Deck 3.

```bash
dotnet build
dotnet test
macrodeck-plugin test --project src/NvidiaVramMonitor --report markdown --output conformance.md
macrodeck-plugin build --source src/NvidiaVramMonitor --output ./artifacts
macrodeck-plugin inspect --artifact ./artifacts/com.yussefabdelwahab.nvidia-smi-tool-1.0.1.macroDeckPlugin
```

Publish the source as a public repository at [YuziIV/MacroDeck-vram-monitor](https://github.com/YuziIV/MacroDeck-vram-monitor). In the [Creator Portal](https://docs.macro-deck.app/creator-portal/publish-plugin/), use a **Plugin / Integration** Project with Package ID `com.yussefabdelwahab.nvidia-smi-tool`, then connect this repository under **Builds**. Publish a new GitHub release tagged `v1.0.1`: `v1.0.0` points to the old package ID, so rerunning its workflow cannot fix the upload. Publishing the new release runs `.github/workflows/release.yml`, which builds and uploads the plugin without a publishing secret. In the Portal, select the build, create a release, add it to a submission and submit for review. Check that the Portal owner shown for the Project matches `publisher.name` in the manifest. The Store requires passing conformance reports on every declared platform and a supported Macro Deck SDK. Generated artifacts and local debug state are ignored by Git; upload source, not `bin/`, `obj/` or a local credential.

Macro Deck packages are pinned together at `3.0.0-beta.14` in `Directory.Packages.props`, above the [Store SDK minimum](https://api.macro-deck.app/api/v1/public/dependency-policy/sdk) of `3.0.0-beta.12`. Check the build's dependency report under **Builds** in the Creator Portal before submitting for review.

The installed package is run and authorized by Macro Deck. The `Properties/launchSettings.json` profile is only for interactive developer debugging against a locally running host; it is not used by store installs.

## License

MIT. See [LICENSE](LICENSE).
