# reMarkable OneNote Addin
This is a OneNote AddIn for importing digitized notes from the reMarkable tablet.

## Version 5.0
Updated to work with the reMarkable cloud API changes in 2025/2026:

- **Cloud sync API**: Updated from the deprecated sync v2 signed-URL protocol to the current v3/v4 endpoints (`/sync/v4/root`, `/sync/v3/files/{hash}`).
- **rm-filename header**: Added the required `rm-filename` header on all blob requests, including the `.docSchema` extension on document index fetches (required as of May 2026).
- **Full colour rendering**: All pen colours now render correctly — black, grey, white, yellow, green, pink, blue and red — for both v5 and v6 `.rm` file formats.
- **Highlight rendering**: Highlighter strokes now render as semi-transparent overlays (25% opacity) at the correct colour, drawn beneath ink strokes so underlying text remains legible through the highlight.
- **Newer firmware colours**: Added support for the extended colour palette introduced in firmware 3.x (HIGHLIGHT, GREEN_2, CYAN, MAGENTA, YELLOW_2). For firmware 3.x files, the exact RGBA colour stored in the `.rm` file is used directly.

Known limitations:
* Text added to a notebook via the reMarkable text tool is not yet rendered.
* When text is added to a notebook, the layout of the generated images may be incorrect.

## Version 4.0
Updated the original version to support the new reMarkable v6 file format.
Decoding the new format was based on the work of [Rick Lupton](https://github.com/ricklupton/rmscene).

## Version 3.0
Version 3 is a major version update that added support for the new (2022) reMarkable cloud API.

Big shoutout to [juruen/rmapi](https://github.com/juruen/rmapi) for working out all the nitty gritty of the new API.

Please note that while the new cloud API is now supported, due to the way it works, it is **SIGNIFICANTLY** slower than the previous cloud API. For example, getting the whole document tree using the previous API required only a single HTTPS request, regardless of how many documents are in the tree. Using the new API, it now requires 4 HTTPS requests for **EACH** document/folder (i.e. if your device has 200 documents, 800 HTTPS requests are needed to build up the document tree). While optimizations such as caching and multithreaded requests are used, it is still much slower to populate the document tree, especially the first time the AddIn syncs.

## Installation
Choose either the 32-bit or the 64-bit Windows installer depending on your version of OneNote:  
In OneNote, go to File menu -> Account -> About OneNote. The first line should say something like "Microsoft OneNote ... 64-bit or 32-bit".

Once the installer has completed, start OneNote. You should see a new tab in the ribbon bar called "reMarkable".

## Setup
Before using the addin, there is a one-time setup to link the addin to your reMarkable tablet and optionally configure the handwriting recognition service.

### Linking your reMarkable tablet
Go to the reMarkable tab in the OneNote ribbon and click the Settings icon.  
Go to <https://my.remarkable.com/device/desktop/connect> to generate a one-time code. Copy it into the "One Time Code" field in the Settings window and click Apply. This links OneNote as a new desktop client to your reMarkable Cloud account.

### Configure handwriting recognition (optional)
The addin uses MyScript (<https://www.myscript.com/>) to convert handwritten strokes into text — the same service used by the reMarkable device itself.

MyScript provides 2000 free conversions per month. Register at <https://sso.myscript.com/register>, then go to <https://developer.myscript.com/getting-started/web>, select the Web platform and click "Send email" to receive your application key and HMAC key. Copy both keys into the corresponding fields in the Settings window and click Apply.

## Troubleshooting
The addin does not produce logs by default. To enable logging, open the Windows Registry and navigate to:
* `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Office\OneNote\AddInsData\RemarkableSync.OnenoteAddin` (64-bit install)
* `HKEY_CURRENT_USER\SOFTWARE\Wow6432Node\Microsoft\Office\OneNote\AddInsData\RemarkableSync.OnenoteAddin` (32-bit install)

Add a new string value named `LogFile` set to the full path where logs should be written, e.g. `C:\temp\remarkable_sync_log.txt`. Restart OneNote for the change to take effect.

## Technical details
Since OneNote only supports out-of-process COM addins, this addin runs as a C# local server COM class. While the addin is loaded you will see a `RemarkableSync.OnenoteAddin.exe` process running alongside `ONENOTE.EXE`.

When the Fetch window is opened, the addin retrieves all notebooks synced to reMarkable Cloud and displays them in a tree. Selecting a notebook and clicking Import will download the `.rm` files for all pages, parse the pen strokes, and insert them as images into the current OneNote page.

## Acknowledgements
- [MyScript](https://www.myscript.com) for the handwriting recognition service.
- [reHackable/awesome-reMarkable](https://github.com/reHackable/awesome-reMarkable) for compiling the list of reMarkable community projects.
- [ddvk/rmfakecloud](https://github.com/ddvk/rmfakecloud) for introducing MyScript as the handwriting recognition service used by reMarkable.
- [ddvk/rmapi](https://github.com/ddvk/rmapi) for tracking the reMarkable cloud API and sync protocol changes.
- [subutux/rmapy](https://github.com/subutux/rmapy) for the initial reMarkable cloud interface.
- [bsdz/remarkable-layers](https://github.com/bsdz/remarkable-layers) for the Python parser for the v5 `.rm` format, which was ported to C# for this project.
- [Lim Bio Liong at CodeProject](https://www.codeproject.com/Articles/12579/Building-COM-Servers-in-NET) for the boilerplate code for creating a C# .NET COM server.
- [juruen/rmapi](https://github.com/juruen/rmapi) for the original new cloud API implementation.
- [ricklupton/rmscene](https://github.com/ricklupton/rmscene) for decoding the v6 binary format and colour model.
