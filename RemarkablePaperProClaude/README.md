# RemarkablePaperProClaude

Ask **Claude** about a handwritten page on your **reMarkable** (Paper Pro / rM1 / rM2).

You write a question by hand in a notebook called **"Claude"**, run this tool,
and Claude's answer comes back as a PDF.

It connects to your reMarkable the **same way the OneNote add-in does** — through
your **reMarkable cloud account**, linked once with a one-time **connect code**.
No SSH or USB cable needed.

```
 ┌─────────────┐   cloud sync   ┌──────────────────────┐   Vision API   ┌────────┐
 │ reMarkable  │ ─────────────▶ │ RemarkablePaperPro   │ ─────────────▶ │ Claude │
 │ notebook    │  (connect      │ Claude (this tool)   │ ◀───────────── │        │
 │ "Claude"    │   code)        │ render → ask → PDF   │   answer text  └────────┘
 └─────────────┘                └──────────────────────┘
```

## How it works

1. Links to your reMarkable cloud account with a one-time connect code (reusing
   the `RemarkableSync` library's cloud client). The token is saved, so you only
   enter a code once.
2. Finds the notebook named `Claude` and renders its **latest page** to a PNG —
   no OCR setup needed; **Claude reads the handwriting directly** with its vision
   API.
3. Sends the page image to the Claude Messages API and gets a text answer.
4. Saves the answer as a PDF (and as plain text). Optionally pushes the PDF back
   onto the device over SSH.

## Setup

### 1. Get a reMarkable connect code
Go to <https://my.remarkable.com/device/desktop/connect> while signed in to your
reMarkable account and generate a one-time code. (Codes expire after a few
minutes, so grab one right before the first run.)

### 2. Get an Anthropic API key
Create one at <https://console.anthropic.com>. The tool defaults to the
`claude-opus-4-8` model.

### 3. Build
Open `RemarkableSync.sln` in Visual Studio and build the
`RemarkablePaperProClaude` project (targets .NET Framework 4.8, same as the rest
of the solution).

## Usage

**First run** — link your account:

```
RemarkablePaperProClaude --connect-code <code> --api-key <anthropic-key>
```

**Later runs** — the saved token is reused, no code needed:

```
RemarkablePaperProClaude --api-key <anthropic-key>
```

(You can also set `ANTHROPIC_API_KEY` and drop `--api-key`.)

### Options

| Option | Description |
| --- | --- |
| `--connect-code <code>` | One-time code to link your reMarkable cloud account. Only needed once. |
| `--api-key <key>` | Anthropic API key (default from `ANTHROPIC_API_KEY`). |
| `--model <id>` | Claude model id (default `claude-opus-4-8`). |
| `--notebook <name>` | Trigger notebook name (default `Claude`). |
| `--task <name>` | Force a task instead of detecting a keyword (see below). |
| `--language <lang>` | Target language for the `translate` task (default `English`). |
| `--out <dir>` | Where to save artifacts (default: current directory). |
| `--config <path>` | Where the saved cloud token lives (default `%APPDATA%\RemarkablePaperProClaude\config.json`). |
| `--ssh-host <ip>` | *Optional:* also push the answer onto the device over SSH. |
| `--ssh-password <pw>` | *Optional:* SSH password for `--ssh-host`. |
| `--list-tasks` | Show the available tasks and their keywords. |
| `-h`, `--help` | Show help. |

## Getting the answer onto the device

The reMarkable cloud API is **read-only** in this tool, so the answer can't be
uploaded back through the cloud. You have two options:

- **Import the PDF** (`claude-answer.pdf`) via the reMarkable desktop/mobile app
  or the cloud — simplest, no extra setup.
- **Push over SSH** (Paper Pro / rM with developer SSH): pass `--ssh-host` and
  `--ssh-password` (the SSH password is under Settings → Help → About). The tool
  then uploads the PDF straight into the device and restarts the UI so it appears.

The rendered question (`question.png`), the answer text (`claude-answer.txt`) and
the answer PDF (`claude-answer.pdf`) are always saved locally either way.

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

- **Cloud sync delay:** pages appear here only after the device has synced them to
  the reMarkable cloud. Give it a moment after writing before running the tool.
- **One page at a time:** the tool reads the *latest* page of the notebook. Start
  a fresh page for each new question.
- **Privacy:** the page image is sent to the Anthropic API. Don't use it for
  pages you don't want to leave your account.
- **SSH write-back format:** if you use `--ssh-host`, writing the document into the
  on-device store uses the community-standard layout, which reMarkable changes
  between firmware versions. If the pushed PDF doesn't show up, just import the
  saved `claude-answer.pdf` via the app instead.

## Ideas for extending

- Watch mode: poll the notebook and answer automatically when a new page is saved.
- Add your own tasks by editing `TaskLibrary.cs` (name, keywords, instruction).
