# RemarkablePaperProClaude

Ask **Claude** about a handwritten page on your **reMarkable** (Paper Pro / rM1 / rM2).

You write a question by hand in a notebook called **"Claude"**, run this tool, and
Claude's answer is uploaded back to your reMarkable as a PDF — fully over the
cloud, no SSH or USB cable.

It uses [**rmapi**](https://github.com/ddvk/rmapi) for both directions of the
reMarkable cloud: rmapi can **download and upload** (the built-in RemarkableSync
cloud client is download-only), so the whole round trip works through the cloud.

```
 ┌─────────────┐   rmapi get    ┌──────────────────────┐  Vision API   ┌────────┐
 │ reMarkable  │ ─────────────▶ │ RemarkablePaperPro   │ ────────────▶ │ Claude │
 │ notebook    │                │ Claude (this tool)   │ ◀──────────── │        │
 │ "Claude"    │ ◀───────────── │ render → ask → PDF   │  answer text  └────────┘
 └─────────────┘   rmapi put    └──────────────────────┘
```

## How it works

1. `rmapi get` downloads the notebook named `Claude` and the tool unpacks it.
2. Renders its **latest page** to a PNG using the `RemarkableSync` `.rm` renderer —
   no OCR setup needed; **Claude reads the handwriting directly** with its vision API.
3. Sends the page image to the Claude Messages API and gets a text answer.
4. Builds a PDF of the answer and `rmapi put`s it back into your cloud, where it
   syncs to the device.

## Setup

### 1. Install and link rmapi
Download rmapi from <https://github.com/ddvk/rmapi> (or `go install
github.com/ddvk/rmapi@latest`) and put it on your `PATH`. Run it once and link it
to your account with a one-time code from
<https://my.remarkable.com/device/desktop/connect>:

```
rmapi        # follow the prompt, paste the connect code
```

rmapi stores its token (in `~/.rmapi`), so you only do this once. This tool then
reuses that authentication — it never asks for a code itself.

### 2. Get an Anthropic API key
Create one at <https://console.anthropic.com>. The tool defaults to the
`claude-opus-4-8` model.

### 3. Build
Open `RemarkableSync.sln` in Visual Studio and build the
`RemarkablePaperProClaude` project (targets .NET Framework 4.8, same as the rest
of the solution).

## Usage

```
RemarkablePaperProClaude --api-key <anthropic-key>
```

(You can also set `ANTHROPIC_API_KEY` and drop `--api-key`.)

### Options

| Option | Description |
| --- | --- |
| `--api-key <key>` | Anthropic API key (default from `ANTHROPIC_API_KEY`). |
| `--model <id>` | Claude model id (default `claude-opus-4-8`). |
| `--notebook <path>` | rmapi path of the trigger notebook (default `Claude`). Use a full path like `/Folder/Claude` if it's nested. |
| `--task <name>` | Force a task instead of detecting a keyword (see below). |
| `--language <lang>` | Target language for the `translate` task (default `English`). |
| `--out <dir>` | Where to save artifacts (default: current directory). |
| `--rmapi <path>` | Path to the rmapi executable (default `rmapi` on PATH). |
| `--rmapi-dest <dir>` | Cloud folder to upload the answer into (default `/`). Must already exist. |
| `--list-tasks` | Show the available tasks and their keywords. |
| `-h`, `--help` | Show help. |

### Typical flow

1. Create a notebook named **Claude** (once) and let it sync.
2. Write your question on a new page; wait a moment for it to sync to the cloud.
3. Run the tool.
4. The answer appears as a **"Claude … "** PDF in your reMarkable (top level, or
   `--rmapi-dest` folder).

## Tasks (keywords)

Write a **command keyword on the first line** of the page to choose what Claude
does with the rest. If the first line isn't a recognised keyword, Claude just
**answers** the page as a question (the default). You can also force a task with
`--task <name>`, which skips keyword detection.

| Task | Keywords | What it does |
| --- | --- | --- |
| `answer` *(default)* | answer, question, ask, q | Answer the question(s) on the page. |
| `summarize` | summarize, summary, tldr | Summarize the page. |
| `cleanup` | cleanup, clean, tidy, transcribe, rewrite | Transcribe and tidy the notes into clean prose. |
| `expand` | expand, outline, flesh | Expand a rough outline into full prose. |
| `translate` | translate, translation | Translate the page (target set by `--language`). |
| `explain` | explain, eli5 | Explain the concept/term/problem on the page. |
| `todo` | todo, tasks, actions | Extract action items as a numbered list. |

Example — handwrite this on the page:

```
summarize
<your meeting notes ...>
```

…and Claude returns a summary instead of an answer. The detection is done by
Claude as it reads the page (no on-device OCR), so the keyword can be in your own
handwriting and is matched case- and punctuation-insensitively.

## Notes & limitations

- **rmapi is required** and must be authenticated first (see Setup). The tool shells
  out to it; if it isn't on the PATH, pass `--rmapi <path>`.
- **Cloud sync delay:** pages appear here only after the device has synced them to
  the reMarkable cloud, and the answer appears on the device after it syncs back.
  Give it a moment in each direction.
- **One page at a time:** the tool reads the *latest* page of the notebook. Start a
  fresh page for each new question.
- **Privacy:** the page image is sent to the Anthropic API. Don't use it for pages
  you don't want to leave your account.
- The rendered question (`question.png`), the answer text (`claude-answer.txt`) and
  the answer PDF are also saved locally in `--out`.

## Ideas for extending

- Watch mode: poll the notebook and answer automatically when a new page is saved.
- Add your own tasks by editing `TaskLibrary.cs` (name, keywords, instruction).
