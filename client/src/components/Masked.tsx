/**
 * Masks sensitive values inside diffs: render-node tokens show only their
 * tail, everything else renders as-is.
 */
export const Masked = {
  mask(path: string, value: string | null): string {
    if (value === null || value === undefined) return ''
    if (path === 'nodeToken') {
      return value.startsWith('••••') ? value : `••••${value.slice(-4)}`
    }
    return value
  },
}
