import { useEffect, useState } from 'react'
import type { AssetConfig, OutputGroup } from '../api/types'
import { api } from '../api/client'
import { OutputChip } from './RenderLog'
import { Field, Input, Toggle } from './ui'

export function ConfigEditor({ draft, onChange }: {
  draft: AssetConfig
  onChange: (next: AssetConfig) => void
}) {
  const [catalog, setCatalog] = useState<OutputGroup[]>([])

  useEffect(() => {
    api.outputs().then(setCatalog).catch(() => setCatalog([]))
  }, [])

  const patch = (partial: Partial<AssetConfig>) => onChange({ ...draft, ...partial })

  const toggleOutput = (output: string) => {
    const has = draft.outputs.includes(output)
    patch({ outputs: has ? draft.outputs.filter((o) => o !== output) : [...draft.outputs, output] })
  }

  const [matKey, setMatKey] = useState('')
  const [matShader, setMatShader] = useState('')
  const addMaterial = () => {
    const name = matKey.trim()
    if (!name) return
    patch({ materials: { ...draft.materials, [name]: matShader.trim() } })
    setMatKey('')
    setMatShader('')
  }

  return (
    <div className="flex flex-col gap-5" data-testid="config-editor">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-[1fr_140px]">
        <Field label="Source file" hint="the .blend this asset is built from">
          <Input
            value={draft.sourceFile}
            onChange={(e) => patch({ sourceFile: e.target.value })}
            placeholder="//characters/bella/bella_rig.blend"
            data-testid="input-source"
          />
        </Field>
        <Field label="Blender" hint="target version">
          <Input
            value={draft.blenderVersion}
            onChange={(e) => patch({ blenderVersion: e.target.value })}
            placeholder="4.2.3"
          />
        </Field>
      </div>

      <Field label="Description">
        <Input
          value={draft.description}
          onChange={(e) => patch({ description: e.target.value })}
          placeholder="What this asset is for"
        />
      </Field>

      <div>
        <p className="mb-2 text-xs font-medium text-dim">
          Render outputs <span className="text-faint">— {draft.outputs.length} enabled</span>
        </p>
        <div className="flex flex-col gap-3 rounded-xl border border-line bg-panel-2/40 p-3.5" data-testid="outputs-picker">
          {catalog.map((g) => (
            <div key={g.group} className="flex flex-wrap items-center gap-1.5">
              <span className="w-20 shrink-0 font-mono text-[10.5px] uppercase tracking-wide text-faint">
                {g.group}
              </span>
              {g.outputs.map((output) => {
                const selected = draft.outputs.includes(output)
                return (
                  <button
                    key={output}
                    type="button"
                    aria-pressed={selected}
                    onClick={() => toggleOutput(output)}
                    className={`rounded-md border px-1.5 py-0.5 font-mono text-[11px] leading-4 transition-all duration-150 ${
                      selected
                        ? 'border-accent/40 bg-accent/10 text-accent'
                        : 'border-line bg-panel-2 text-faint hover:border-[#33415c] hover:text-dim'
                    }`}
                  >
                    {output}
                  </button>
                )
              })}
            </div>
          ))}
          {draft.outputs.length > 0 && (
            <div className="flex flex-wrap items-center gap-1.5 border-t border-line-soft pt-3">
              <span className="w-20 shrink-0 font-mono text-[10.5px] uppercase tracking-wide text-faint">on</span>
              {draft.outputs.map((o) => <OutputChip key={o} output={o} />)}
            </div>
          )}
        </div>
      </div>

      <div className="grid grid-cols-3 gap-4">
        <Field label="Engine">
          <select
            value={draft.engine}
            onChange={(e) => patch({ engine: e.target.value })}
            className="h-9 w-full rounded-lg border border-line bg-panel-2 px-2.5 text-sm"
          >
            <option value="CYCLES">CYCLES</option>
            <option value="EEVEE">EEVEE</option>
          </select>
        </Field>
        <Field label="Samples" hint="1–4096">
          <Input
            type="number" min={1} max={4096}
            value={draft.render.samples}
            onChange={(e) => patch({ render: { ...draft.render, samples: Number(e.target.value) } })}
            data-testid="input-samples"
          />
        </Field>
        <div className="flex items-end pb-1">
          <div className="flex w-full items-center gap-3 rounded-lg border border-line bg-panel-2/40 px-3 py-2">
            <Toggle
              on={draft.render.denoise}
              onChange={(next) => patch({ render: { ...draft.render, denoise: next } })}
              label="Denoising"
            />
            <span className="text-xs text-dim">Denoise</span>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <Field label="Resolution X">
          <Input
            type="number" min={16} max={8192}
            value={draft.resX}
            onChange={(e) => patch({ resX: Number(e.target.value) })}
          />
        </Field>
        <Field label="Resolution Y">
          <Input
            type="number" min={16} max={8192}
            value={draft.resY}
            onChange={(e) => patch({ resY: Number(e.target.value) })}
          />
        </Field>
      </div>

      <div>
        <p className="mb-2 text-xs font-medium text-dim">Materials</p>
        <div className="flex flex-col gap-1.5 rounded-xl border border-line bg-panel-2/40 p-3">
          {Object.entries(draft.materials).length === 0 && (
            <p className="text-xs text-faint">No materials tracked yet.</p>
          )}
          {Object.entries(draft.materials).map(([name, shader]) => (
            <div key={name} className="flex items-center gap-2 text-xs">
              <code className="rounded-md border border-line bg-panel-2 px-2 py-1 font-mono text-text">{name}</code>
              <code className="min-w-0 flex-1 truncate rounded-md border border-line-soft bg-panel-2 px-2 py-1 font-mono text-dim">{shader}</code>
              <button
                type="button"
                aria-label={`Remove material ${name}`}
                onClick={() => {
                  const next = { ...draft.materials }
                  delete next[name]
                  patch({ materials: next })
                }}
                className="grid h-6 w-6 shrink-0 place-items-center rounded-md text-faint transition-colors hover:bg-bad/10 hover:text-bad"
              >
                ✕
              </button>
            </div>
          ))}
          <div className="mt-1 flex items-center gap-2">
            <Input
              className="h-8 flex-1 font-mono text-xs"
              placeholder="Material name"
              value={matKey}
              onChange={(e) => setMatKey(e.target.value)}
              data-testid="input-material-name"
            />
            <Input
              className="h-8 flex-[1.4] font-mono text-xs"
              placeholder="Shader (Principled BSDF…)"
              value={matShader}
              onChange={(e) => setMatShader(e.target.value)}
            />
            <button
              type="button"
              onClick={addMaterial}
              className="h-8 shrink-0 rounded-lg border border-line bg-panel-2 px-3 text-xs text-dim transition-colors hover:border-accent/40 hover:text-accent"
            >
              Add
            </button>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-[1fr_auto] gap-4">
        <Field label="Render-node token" hint="farm auth; masked in diffs">
          <Input
            className="font-mono text-xs"
            value={draft.nodeToken}
            onChange={(e) => patch({ nodeToken: e.target.value })}
            placeholder="rnd_…"
          />
        </Field>
        <div className="flex items-end pb-1">
          <div className="flex items-center gap-3 rounded-lg border border-line bg-panel-2/40 px-3.5 py-2">
            <Toggle
              on={draft.isActive}
              onChange={(next) => patch({ isActive: next })}
              label="Asset active"
            />
            <span className="text-xs text-dim">
              {draft.isActive ? 'Active — renders allowed' : 'Locked — renders are blocked'}
            </span>
          </div>
        </div>
      </div>

      <Field label="Output naming pattern" hint="{{version}}, {{output}} and #### (frame) are filled in">
        <textarea
          value={draft.outputPattern}
          onChange={(e) => patch({ outputPattern: e.target.value })}
          rows={2}
          spellCheck={false}
          placeholder="//renders/{{asset}}/v{{version}}/{{output}}_####"
          className="w-full rounded-lg border border-line bg-panel-2 p-3 font-mono text-xs text-dim focus-visible:border-accent/50"
        />
      </Field>
    </div>
  )
}
