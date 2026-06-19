# RemarkablePaperProClaude

Ask **Claude** about a handwritten page on your **reMarkable Paper Pro** (or rM1/rM2).

You write a question by hand in a notebook called **"Claude"**, run this tool,
and Claude's answer is dropped back onto your tablet as a new PDF document.

```
 ┌─────────────┐   SSH/SFTP   ┌──────────────────────┐   Vision API   ┌────────┐
 │ Paper Pro   │ ───────────▶ │ RemarkablePaperPro   │ ─────────────▶ │ Claude │
 │ notebook    │              │ Claude (this tool)   │ ◀───────────── │        │
 │ "Claude"    │ ◀─────────── │ render → ask → write │   answer text  └────────┘
 └─────────────┘   new PDF     └──────────────────────┘
```

## How it works

1. Connects to the tablet over SSH/SFTP (reusing the `RemarkableSync` library).
2. Finds the notebook named `Claude` and renders its **latest page** to a PNG —
   no on-device OCR is needed; **Claude reads the handwriting directly** with its
   vision API.
3. Sends the page image to the Claude Messages API and gets a text answer.
4. Generates a PDF of the answer and uploads it back to the tablet as a new
   document, then restarts the UI so it appears.

The rendered question (`question.png`), the answer text (`claude-answer.txt`) and
the answer PDF (`claude-answer.pdf`) are always saved locally too, so you have the
result even if device write-back needs tweaking for your firmware.

## Setup

### 1. Enable SSH on the tablet
On the device: **Settings → Help → About → Copyrights and licenses** (or
**General information**) shows the **SSH password** and the IP address. Over USB
the tablet is reachable at `10.11.99.1`.

### 2. Get an Anthropic API key
Create one at <https://console.anthropic.com>. The tool defaults to the
`claude-opus-4-8` model.

### 3. Build
Open `RemarkableSync.sln` in Visual Studio and build the
`RemarkablePaperProClaude` project (targets .NET Framework 4.8, same as the rest
of the solution).

## Usage

```
RemarkablePaperProClaude --host 10.11.99.1 --password <ssh-password> --api-key <anthropic-key>
```

Or set environment variables and just run it:

```
set REMARKABLE_SSH_HOST=10.11.99.1
set REMARKABLE_SSH_PASSWORD=xxxxxxxx
set ANTHROPIC_API_KEY=sk-ant-...
RemarkablePaperProClaude
```

### Options

| Option | Description |
| --- | --- |
| `--host <ip>` | Tablet IP (default from `REMARKABLE_SSH_HOST`). |
| `--password <pw>` | SSH password (default from `REMARKABLE_SSH_PASSWORD`). |
| `--api-key <key>` | Anthropic API key (default from `ANTHROPIC_API_KEY`). |
| `--model <id>` | Claude model id (default `claude-opus-4-8`). |
| `--notebook <name>` | Trigger notebook name (default `Claude`). |
| `--task <name>` | Force a task instead of detecting a keyword (see below). |
| `--language <lang>` | Target language for the `translate` task (default `English`). |
| `--out <dir>` | Where to save artifacts (default: current directory). |
| `--no-write-back` | Don't upload the answer to the device; just print and save locally. |
| `--list-tasks` | Show the available tasks and their keywords. |
| `-h`, `--help` | Show help. |

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

### Typical flow

1. On the tablet, create a notebook named **Claude** (once).
2. Write your question on a new page.
3. Run the tool (`--no-write-back` first if you just want to test reading).
4. The answer appears as a **"Claude answer - …"** PDF in the same folder as the
   notebook.

## Notes & limitations

- **Firmware format:** writing a document into the on-device store
  (`/home/root/.local/share/remarkable/xochitl`) uses the community-standard
  metadata/content layout. reMarkable changes this format between releases; if a
  future Paper Pro firmware doesn't show the uploaded PDF, run with
  `--no-write-back` and copy `claude-answer.pdf` across by USB instead. The
  metadata/content shape lives in `RmDeviceWriter.cs` and is easy to adjust.
- **One page at a time:** the tool reads the *latest* page of the notebook. Start
  a fresh page for each new question.
- **Privacy:** the page image is sent to the Anthropic API. Don't use it for
  pages you don't want to leave the device.
- **No SDK dependency:** the Claude call is a single documented HTTPS request
  (`ClaudeClient.cs`) to keep this .NET Framework tool's dependencies minimal.

## Ideas for extending

- Watch mode: poll the notebook and answer automatically when a new page is saved.
- Append the answer to the *same* notebook instead of a new document.
- Add your own tasks by editing `TaskLibrary.cs` (name, keywords, instruction).
