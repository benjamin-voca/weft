import '@testing-library/jest-dom/vitest'

// jsdom lacks layout APIs some animation code touches — keep framer-motion quiet.
window.matchMedia ??= ((query: string) => ({
  matches: false, media: query, onchange: null,
  addListener: () => {}, removeListener: () => {},
  addEventListener: () => {}, removeEventListener: () => {},
  dispatchEvent: () => false,
})) as typeof window.matchMedia

Element.prototype.scrollIntoView ??= () => {}
