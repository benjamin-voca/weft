import { motion } from 'framer-motion'

export function WeftMark({ size = 30, animated = true }: { size?: number; animated?: boolean }) {
  return (
    <svg width={size} height={size} viewBox="0 0 30 30" fill="none" aria-hidden>
      <rect width="30" height="30" rx="8" fill="#101825" />
      <rect x="0.5" y="0.5" width="29" height="29" rx="7.5" stroke="#3ad9c5" strokeOpacity="0.25" />
      {animated && (
        <motion.path
          d="M8 9c4 0 4 12 8 12 3 0 4-5 6-5"
          stroke="#3ad9c5"
          strokeWidth="1.8"
          strokeLinecap="round"
          initial={{ pathLength: 0 }}
          animate={{ pathLength: 1 }}
          transition={{ duration: 1, ease: 'easeOut' }}
        />
      )}
      {!animated && (
        <path
          d="M8 9c4 0 4 12 8 12 3 0 4-5 6-5"
          stroke="#3ad9c5"
          strokeWidth="1.8"
          strokeLinecap="round"
        />
      )}
      <path
        d="M8 15c4 0 4-4 8-4 3 0 4 8 6 8"
        stroke="#a78bfa"
        strokeOpacity="0.7"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  )
}
