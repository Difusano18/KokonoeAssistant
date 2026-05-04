// SVG icon set — outline, 16px default, currentColor
const Icon = {
  chat: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M2.5 4.5a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2H7l-3 2.5v-2.5h-.5a2 2 0 0 1-2-2v-5z"/>
    </svg>
  ),
  vault: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M2 4.5a1.5 1.5 0 0 1 1.5-1.5H6l1.5 1.5h5A1.5 1.5 0 0 1 14 6v5.5a1.5 1.5 0 0 1-1.5 1.5h-9A1.5 1.5 0 0 1 2 11.5v-7z"/>
    </svg>
  ),
  cal: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <rect x="2.5" y="3.5" width="11" height="10" rx="1.5"/>
      <path d="M2.5 6.5h11M5.5 2v3M10.5 2v3"/>
    </svg>
  ),
  dash: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M8 2L3 8.5h10L8 2zM5 9.5l3 4 3-4"/>
    </svg>
  ),
  tg: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M14 3L1.5 8l4 1.5M14 3l-2 10-4.5-4M14 3L7.5 9.5l-2 4.5"/>
    </svg>
  ),
  send: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.6" {...p}>
      <path d="M2 8L14 2L9 14L7.5 9L2 8z"/>
    </svg>
  ),
  search: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <circle cx="7" cy="7" r="4.5"/><path d="M10.5 10.5L14 14"/>
    </svg>
  ),
  plus: (p={}) => (
    <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.6" {...p}>
      <path d="M8 3v10M3 8h10"/>
    </svg>
  ),
  attach: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M11 5L5.5 10.5a2 2 0 1 0 3 3L13 9a4 4 0 1 0-5.5-5.5L3 8"/>
    </svg>
  ),
  pin: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M5.5 1.5l5 5L13 6l-3 3.5-3.5-3.5L3 8.5l5-5"/><path d="M3 13l3-3"/>
    </svg>
  ),
  trash: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M3 4.5h10M6 4.5V3a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v1.5M4.5 4.5v8a1 1 0 0 0 1 1h5a1 1 0 0 0 1-1v-8"/>
    </svg>
  ),
  cog: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <circle cx="8" cy="8" r="2.2"/>
      <path d="M8 1.5v2M8 12.5v2M3.4 3.4l1.4 1.4M11.2 11.2l1.4 1.4M1.5 8h2M12.5 8h2M3.4 12.6l1.4-1.4M11.2 4.8l1.4-1.4"/>
    </svg>
  ),
  min: (p={}) => <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}><path d="M3 8h10"/></svg>,
  max: (p={}) => <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}><rect x="3" y="3" width="10" height="10"/></svg>,
  close: (p={}) => <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}><path d="M3 3l10 10M13 3L3 13"/></svg>,
  folder: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M2 4a1 1 0 0 1 1-1h3l1.5 1.5H13a1 1 0 0 1 1 1V12a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V4z"/>
    </svg>
  ),
  file: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M3.5 2h6L13 5.5V14a.5.5 0 0 1-.5.5h-9A.5.5 0 0 1 3 14V2.5a.5.5 0 0 1 .5-.5z"/>
      <path d="M9 2v4h4"/>
    </svg>
  ),
  caret: (p={}) => (
    <svg viewBox="0 0 16 16" width="10" height="10" fill="currentColor" {...p}>
      <path d="M5 4l5 4-5 4z"/>
    </svg>
  ),
  memory: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <rect x="2" y="3" width="12" height="10" rx="1.5"/>
      <path d="M5 6h6M5 8h6M5 10h4"/>
      <circle cx="13" cy="3" r="1.2" fill="currentColor"/>
    </svg>
  ),
  pulse: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M1.5 8h3l1.5-4 2 8 1.5-5 1 2h4"/>
    </svg>
  ),
  ritual: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <circle cx="8" cy="8" r="6"/>
      <path d="M8 4v4l2.5 2.5"/>
    </svg>
  ),
  sandbox: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M3 3h4v4H3zM9 3h4v4H9zM3 9h4v4H3zM9 9h4v4H9z"/>
    </svg>
  ),
  voice: (p={}) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <rect x="6.5" y="2" width="3" height="8" rx="1.5"/>
      <path d="M3.5 7.5a4.5 4.5 0 0 0 9 0M8 12v2"/>
    </svg>
  ),
  graph: (p={}) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <circle cx="3" cy="3" r="1.5"/><circle cx="13" cy="4" r="1.5"/><circle cx="8" cy="13" r="1.5"/>
      <circle cx="8" cy="7" r="1.8"/>
      <path d="M4 4l3 2M12 5l-3 1.5M8 9v3"/>
    </svg>
  ),
};

window.Icon = Icon;
