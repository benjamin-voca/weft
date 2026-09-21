# bldr — the Weft command line

`bldr` is the terminal companion to the Weft web console: version control for
your Blender projects, straight from the shell. Same API, same immutable
snapshots, same semantic diffs — no browser needed.

## Install

```bash
# from the repo root — build once, alias forever
dotnet publish src/Weft.Cli -c Release -o ~/.bldr/bin
alias bldr='dotnet ~/.bldr/bin/Weft.Cli.dll'      # add to your ~/.zshrc
```

or run it ad hoc:

```bash
dotnet run --project src/Weft.Cli -- <command>
```

## First run

```bash
bldr login                 # prompts for email + password (hidden input)
bldr whoami                # verify the session
bldr projects              # see what productions exist
bldr use "Coffee Run"      # set your active production
```

The session and active production are stored in `~/.bldr/config.json`
(override the location with the `BLDR_CONFIG` env var; override the server
with `--url http://host:port` on login).

## Typical flow

```bash
bldr add Bella --source //characters/bella/bella_rig.blend
bldr edit Bella --samples 256 --enable turntable
bldr status                       # shows the pending draft
bldr diff Bella                   # field-level diff: draft vs HEAD
bldr commit -m "firstrig"         # snapshot as v2
bldr log Bella                    # the history
bldr render Bella final           # queue a render on the farm
```

`bldr add .` uses the current directory's name as the asset name and
`//dirname/dirname.blend` as the source — handy inside a project folder.

## Command reference

### Session

| command | what it does |
| --- | --- |
| `bldr login [--url u] [--email e] [--password p]` | authenticate; stores the session locally. Missing flags are prompted for (password hidden). |
| `bldr logout` | ends the server session and forgets it locally |
| `bldr whoami` | prints the signed-in user and role |

### Productions

| command | what it does |
| --- | --- |
| `bldr projects` | list productions with asset counts |
| `bldr init <name> [--description "…"]` | create a production and make it active |
| `bldr use <name>` | set the active production (no name: shows the current one) |
| `bldr status` | active production + every asset: version, hash, draft state, renders, success rate |

### Assets — the version control core

| command | what it does |
| --- | --- |
| `bldr add <name\|.\|path> [--source …] [--engine …] [--samples n] [--res WxH] [--outputs "a,b"] [--description "…"]` | register a new asset and commit v1 ("Initial commit") |
| `bldr edit <asset> …` | change the draft — see options below |
| `bldr commit [asset] [-m "msg"] [-a]` | snapshot the draft as a new immutable commit; without `-m` the message is generated from the diff; `-a` commits every dirty asset; with no asset named, commits the single dirty one |
| `bldr discard [asset]` | throw the draft away — working copy returns to HEAD |
| `bldr log <asset> [-n 20]` | commit history, newest first, HEAD marked |
| `bldr diff [asset] [--from vN] [--to vM]` | field-level diff; draft vs HEAD by default, any two versions with `--from/--to` |
| `bldr rollback <asset> <vN> [-m "msg"]` | restore an old snapshot **as a new commit** — history is never rewritten |
| `bldr rm <asset> --yes` | remove an asset and its entire history (irreversible) |

`bldr edit` options (combine freely in one run):

```
--source path             --description "…"
--engine CYCLES|EEVEE     --samples n
--res WxH                 --denoise on|off
--enable output           --disable output      (comma separated ok)
--material "name=shader"  --lock / --unlock
```

### Renders

| command | what it does |
| --- | --- |
| `bldr render <asset> <output> [--data '{"frame":42}']` | queue a render against the current snapshot and print the outcome |
| `bldr renders [asset] [-n 20]` | recent jobs: output, pinned snapshot version, duration, attempts, errors |
| `bldr replay <renderId>` | re-run a past render against the current snapshot |

## How Weft differs from git (on purpose)

- **No staging area.** Each asset has exactly one draft; it auto-saves and
  always diffs against HEAD. `bldr status` shows what's pending.
- **No branches.** Linear history per asset — production pipelines rarely
  want merges, and Weft would rather make rollback instant.
- **Rollback never rewrites.** `bldr rollback Bella v3` creates a new commit
  that restores v3's snapshot — so you can undo your undo.
- **Every render is pinned** to the snapshot it was rendered with, which
  turns "which change broke the frames?" into `bldr renders` + `bldr log`.

## Notes

- Exit codes: `0` success, `1` error, `2` usage mistake.
- `bldr <command> --help` prints per-command usage.
- Colors are automatically disabled when output is piped.
- The CLI talks to the same Minimal API as the web console — anything you do
  in the terminal shows up in the browser and vice versa.
