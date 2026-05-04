// Kokonoe ASI — original anime-style avatar + anatomical cyber-heart
const { useState: aS, useEffect: aE, useMemo: aM } = React;

const AVATAR_EMOTIONS = {
  concerned: { mouth: "concerned", brow: 0.3, blush: 0.4, label: "concerned" },
  steady:    { mouth: "neutral",   brow: 0,   blush: 0.2, label: "steady" },
  curious:   { mouth: "open",      brow: -0.3, blush: 0.3, label: "curious" },
  warm:      { mouth: "smile",     brow: -0.1, blush: 0.6, label: "warm" },
  alert:     { mouth: "alert",     brow: 0.6, blush: 0.1, label: "alert" },
};

// =================== ANIME AVATAR ===================
// Original cel-shaded character — pink-haired researcher, lab coat, glasses
function KokonoeAvatar({ emotion = "concerned", size = 280 }) {
  const e = AVATAR_EMOTIONS[emotion] || AVATAR_EMOTIONS.concerned;
  const [t, setT] = aS(0);
  aE(() => { const id = setInterval(() => setT(x => x + 1), 60); return () => clearInterval(id); }, []);

  // Subtle breathing + blink
  const breath = Math.sin(t * 0.04) * 0.6;
  const blinkPhase = (t * 60) % 4500;
  const blink = blinkPhase < 120 ? 1 - blinkPhase / 60 : blinkPhase < 240 ? (blinkPhase - 120) / 120 : 1;
  const lookX = Math.sin(t * 0.018) * 1.2;
  const lookY = -0.4 + Math.cos(t * 0.012) * 0.6;

  // Hair tail wave
  const tailWave = (i) => Math.sin(t * 0.05 + i * 0.7) * 4;

  const W = size, H = size * 1.15;

  // Mouth path by mood
  const mouthPath = {
    concerned: `M ${W*0.46} ${H*0.585} Q ${W*0.5} ${H*0.578} ${W*0.54} ${H*0.585}`,
    neutral:   `M ${W*0.46} ${H*0.585} L ${W*0.54} ${H*0.585}`,
    smile:     `M ${W*0.455} ${H*0.582} Q ${W*0.5} ${H*0.6} ${W*0.545} ${H*0.582}`,
    open:      `M ${W*0.475} ${H*0.585} Q ${W*0.5} ${H*0.605} ${W*0.525} ${H*0.585} Q ${W*0.5} ${H*0.595} ${W*0.475} ${H*0.585} Z`,
    alert:     `M ${W*0.46} ${H*0.59} Q ${W*0.5} ${H*0.585} ${W*0.54} ${H*0.59}`,
  }[e.mouth];

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} style={{ display: "block" }}>
      <defs>
        {/* Hair gradients */}
        <linearGradient id="kk-hair" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#ff8fb8"/>
          <stop offset="50%" stopColor="#e85a8a"/>
          <stop offset="100%" stopColor="#a83568"/>
        </linearGradient>
        <linearGradient id="kk-hair-tip" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#e85a8a"/>
          <stop offset="80%" stopColor="#ffd0e0"/>
          <stop offset="100%" stopColor="#ffffff"/>
        </linearGradient>
        <linearGradient id="kk-hair-shadow" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0%" stopColor="#7a2548" stopOpacity="0.6"/>
          <stop offset="100%" stopColor="#7a2548" stopOpacity="0"/>
        </linearGradient>
        {/* Skin */}
        <linearGradient id="kk-skin" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#fde2cf"/>
          <stop offset="100%" stopColor="#f0c7ad"/>
        </linearGradient>
        <radialGradient id="kk-blush" cx="50%" cy="50%">
          <stop offset="0%" stopColor="#ff8fa8" stopOpacity={e.blush}/>
          <stop offset="100%" stopColor="#ff8fa8" stopOpacity="0"/>
        </radialGradient>
        {/* Coat */}
        <linearGradient id="kk-coat" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#f5f5f8"/>
          <stop offset="100%" stopColor="#c8c8d0"/>
        </linearGradient>
        <linearGradient id="kk-coat-shade" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0%" stopColor="#9aa0b0" stopOpacity="0.5"/>
          <stop offset="100%" stopColor="#9aa0b0" stopOpacity="0"/>
        </linearGradient>
        {/* Eye */}
        <radialGradient id="kk-eye" cx="50%" cy="40%">
          <stop offset="0%" stopColor="#ffd24a"/>
          <stop offset="60%" stopColor="#e89a1a"/>
          <stop offset="100%" stopColor="#7a4a08"/>
        </radialGradient>
        {/* Background glow */}
        <radialGradient id="kk-bg-glow">
          <stop offset="0%" stopColor="#ff8fb8" stopOpacity="0.15"/>
          <stop offset="60%" stopColor="#ffb020" stopOpacity="0.05"/>
          <stop offset="100%" stopColor="#000" stopOpacity="0"/>
        </radialGradient>
        <filter id="kk-soft"><feGaussianBlur stdDeviation="0.4"/></filter>
      </defs>

      {/* Atmospheric backdrop */}
      <rect width={W} height={H} fill="url(#kk-bg-glow)"/>

      {/* === BACK HAIR (twin tails behind) === */}
      <g transform={`translate(0, ${breath})`}>
        {/* Long back tail flowing right */}
        <path d={`
          M ${W*0.62} ${H*0.32}
          C ${W*0.85 + tailWave(0)} ${H*0.38},
            ${W*0.92 + tailWave(1)} ${H*0.5},
            ${W*0.88 + tailWave(2)} ${H*0.62}
          C ${W*0.84 + tailWave(3)} ${H*0.72},
            ${W*0.78 + tailWave(4)} ${H*0.78},
            ${W*0.72 + tailWave(5)} ${H*0.82}
          L ${W*0.66} ${H*0.55}
          L ${W*0.6} ${H*0.4}
          Z
        `} fill="url(#kk-hair)"/>
        {/* Highlight strands on back tail */}
        <path d={`
          M ${W*0.68} ${H*0.36}
          Q ${W*0.82 + tailWave(0)} ${H*0.46},
            ${W*0.78 + tailWave(2)} ${H*0.62}
        `} fill="none" stroke="url(#kk-hair-tip)" strokeWidth={2} strokeOpacity="0.6"/>
        {/* Tail tip — lighter */}
        <path d={`
          M ${W*0.72 + tailWave(5)} ${H*0.82}
          C ${W*0.7 + tailWave(5)} ${H*0.86},
            ${W*0.68 + tailWave(5)} ${H*0.88},
            ${W*0.65 + tailWave(5)} ${H*0.86}
          L ${W*0.7 + tailWave(5)} ${H*0.78}
          Z
        `} fill="url(#kk-hair-tip)"/>

        {/* Second tail flowing left */}
        <path d={`
          M ${W*0.4} ${H*0.32}
          C ${W*0.18 - tailWave(1)} ${H*0.4},
            ${W*0.1 - tailWave(2)} ${H*0.55},
            ${W*0.16 - tailWave(3)} ${H*0.7}
          C ${W*0.2 - tailWave(4)} ${H*0.78},
            ${W*0.26 - tailWave(5)} ${H*0.82},
            ${W*0.3 - tailWave(5)} ${H*0.84}
          L ${W*0.36} ${H*0.55}
          L ${W*0.4} ${H*0.4}
          Z
        `} fill="url(#kk-hair)"/>
        <path d={`
          M ${W*0.34} ${H*0.36}
          Q ${W*0.2 - tailWave(2)} ${H*0.5},
            ${W*0.22 - tailWave(4)} ${H*0.7}
        `} fill="none" stroke="url(#kk-hair-tip)" strokeWidth={2} strokeOpacity="0.6"/>
        <path d={`
          M ${W*0.3 - tailWave(5)} ${H*0.84}
          C ${W*0.28 - tailWave(5)} ${H*0.88},
            ${W*0.32 - tailWave(5)} ${H*0.9},
            ${W*0.35 - tailWave(5)} ${H*0.86}
          L ${W*0.32 - tailWave(5)} ${H*0.8}
          Z
        `} fill="url(#kk-hair-tip)"/>
      </g>

      {/* === LAB COAT (white, behind shoulders/torso) === */}
      <g transform={`translate(0, ${breath})`}>
        {/* Coat collar / shoulders */}
        <path d={`
          M ${W*0.22} ${H*0.78}
          C ${W*0.22} ${H*0.65}, ${W*0.32} ${H*0.6}, ${W*0.42} ${H*0.62}
          L ${W*0.5} ${H*0.66}
          L ${W*0.58} ${H*0.62}
          C ${W*0.68} ${H*0.6}, ${W*0.78} ${H*0.65}, ${W*0.78} ${H*0.78}
          L ${W*0.78} ${H*1.0}
          L ${W*0.22} ${H*1.0}
          Z
        `} fill="url(#kk-coat)" stroke="#3a3a45" strokeWidth="1.5"/>
        {/* Coat shadow */}
        <path d={`
          M ${W*0.22} ${H*0.78}
          C ${W*0.22} ${H*0.65}, ${W*0.32} ${H*0.6}, ${W*0.42} ${H*0.62}
          L ${W*0.5} ${H*0.66}
          L ${W*0.58} ${H*0.62}
          C ${W*0.68} ${H*0.6}, ${W*0.78} ${H*0.65}, ${W*0.78} ${H*0.78}
          L ${W*0.78} ${H*1.0}
          L ${W*0.22} ${H*1.0}
          Z
        `} fill="url(#kk-coat-shade)"/>
        {/* Inner shirt (red/burgundy) */}
        <path d={`
          M ${W*0.42} ${H*0.62}
          L ${W*0.5} ${H*0.66}
          L ${W*0.58} ${H*0.62}
          L ${W*0.5} ${H*0.78}
          Z
        `} fill="#7a1a2a"/>
        {/* Lapel highlight lines */}
        <path d={`M ${W*0.42} ${H*0.62} L ${W*0.46} ${H*1.0}`} fill="none" stroke="#3a3a45" strokeWidth="1"/>
        <path d={`M ${W*0.58} ${H*0.62} L ${W*0.54} ${H*1.0}`} fill="none" stroke="#3a3a45" strokeWidth="1"/>
      </g>

      {/* === NECK === */}
      <g transform={`translate(0, ${breath})`}>
        <path d={`M ${W*0.43} ${H*0.55} L ${W*0.43} ${H*0.66} L ${W*0.57} ${H*0.66} L ${W*0.57} ${H*0.55} Z`}
          fill="url(#kk-skin)"/>
        {/* Neck shadow */}
        <path d={`M ${W*0.43} ${H*0.6} L ${W*0.57} ${H*0.6} L ${W*0.57} ${H*0.66} L ${W*0.43} ${H*0.66} Z`}
          fill="#d4a48a" opacity="0.5"/>
      </g>

      {/* === HEAD === */}
      <g transform={`translate(0, ${breath})`}>
        {/* Face shape */}
        <path d={`
          M ${W*0.36} ${H*0.36}
          C ${W*0.36} ${H*0.28}, ${W*0.4} ${H*0.22}, ${W*0.5} ${H*0.22}
          C ${W*0.6} ${H*0.22}, ${W*0.64} ${H*0.28}, ${W*0.64} ${H*0.36}
          C ${W*0.64} ${H*0.45}, ${W*0.61} ${H*0.55}, ${W*0.5} ${H*0.58}
          C ${W*0.39} ${H*0.55}, ${W*0.36} ${H*0.45}, ${W*0.36} ${H*0.36}
          Z
        `} fill="url(#kk-skin)"/>

        {/* === FRONT HAIR / BANGS === */}
        {/* Top hair mass */}
        <path d={`
          M ${W*0.34} ${H*0.34}
          C ${W*0.32} ${H*0.22}, ${W*0.38} ${H*0.14}, ${W*0.5} ${H*0.13}
          C ${W*0.62} ${H*0.14}, ${W*0.68} ${H*0.22}, ${W*0.66} ${H*0.34}
          L ${W*0.64} ${H*0.32}
          C ${W*0.62} ${H*0.24}, ${W*0.58} ${H*0.2}, ${W*0.5} ${H*0.2}
          C ${W*0.42} ${H*0.2}, ${W*0.38} ${H*0.24}, ${W*0.36} ${H*0.32}
          Z
        `} fill="url(#kk-hair)"/>

        {/* Bangs — center spike */}
        <path d={`
          M ${W*0.42} ${H*0.22}
          L ${W*0.48} ${H*0.36}
          L ${W*0.5} ${H*0.34}
          L ${W*0.52} ${H*0.36}
          L ${W*0.58} ${H*0.22}
          C ${W*0.55} ${H*0.18}, ${W*0.45} ${H*0.18}, ${W*0.42} ${H*0.22}
          Z
        `} fill="url(#kk-hair)"/>
        {/* Bang side strand left */}
        <path d={`
          M ${W*0.36} ${H*0.28}
          L ${W*0.38} ${H*0.42}
          L ${W*0.41} ${H*0.4}
          L ${W*0.42} ${H*0.3}
          Z
        `} fill="url(#kk-hair)"/>
        {/* Bang side strand right */}
        <path d={`
          M ${W*0.64} ${H*0.28}
          L ${W*0.62} ${H*0.42}
          L ${W*0.59} ${H*0.4}
          L ${W*0.58} ${H*0.3}
          Z
        `} fill="url(#kk-hair)"/>

        {/* Hair highlight */}
        <path d={`
          M ${W*0.4} ${H*0.18}
          Q ${W*0.5} ${H*0.15}, ${W*0.6} ${H*0.18}
          Q ${W*0.55} ${H*0.16}, ${W*0.5} ${H*0.16}
          Q ${W*0.45} ${H*0.16}, ${W*0.4} ${H*0.18}
          Z
        `} fill="#ffd0e0" opacity="0.7"/>

        {/* === EARS (small, behind hair) === */}
        <ellipse cx={W*0.355} cy={H*0.4} rx={W*0.018} ry={H*0.022} fill="url(#kk-skin)"/>
        <ellipse cx={W*0.645} cy={H*0.4} rx={W*0.018} ry={H*0.022} fill="url(#kk-skin)"/>

        {/* === EYES === */}
        <g>
          {/* Left eye */}
          <g transform={`translate(${W*0.435}, ${H*0.4})`}>
            {/* Eye white */}
            <ellipse rx={W*0.038} ry={H*0.022 * blink} fill="#ffffff"/>
            {/* Iris (amber/gold) */}
            <ellipse cx={lookX} cy={lookY} rx={W*0.028} ry={H*0.02 * blink} fill="url(#kk-eye)"/>
            {/* Pupil */}
            <ellipse cx={lookX} cy={lookY} rx={W*0.012} ry={H*0.012 * blink} fill="#1a0d00"/>
            {/* Highlight */}
            <ellipse cx={lookX - W*0.008} cy={lookY - H*0.006} rx={W*0.008} ry={H*0.006 * blink} fill="#fff"/>
            <ellipse cx={lookX + W*0.01} cy={lookY + H*0.006} rx={W*0.004} ry={H*0.003 * blink} fill="#fff" opacity="0.7"/>
            {/* Upper lash */}
            <path d={`M ${-W*0.038} 0 Q 0 ${-H*0.025 * blink} ${W*0.038} 0`} fill="none" stroke="#1a0d00" strokeWidth="1.5" strokeLinecap="round"/>
          </g>
          {/* Right eye */}
          <g transform={`translate(${W*0.565}, ${H*0.4})`}>
            <ellipse rx={W*0.038} ry={H*0.022 * blink} fill="#ffffff"/>
            <ellipse cx={lookX} cy={lookY} rx={W*0.028} ry={H*0.02 * blink} fill="url(#kk-eye)"/>
            <ellipse cx={lookX} cy={lookY} rx={W*0.012} ry={H*0.012 * blink} fill="#1a0d00"/>
            <ellipse cx={lookX - W*0.008} cy={lookY - H*0.006} rx={W*0.008} ry={H*0.006 * blink} fill="#fff"/>
            <ellipse cx={lookX + W*0.01} cy={lookY + H*0.006} rx={W*0.004} ry={H*0.003 * blink} fill="#fff" opacity="0.7"/>
            <path d={`M ${-W*0.038} 0 Q 0 ${-H*0.025 * blink} ${W*0.038} 0`} fill="none" stroke="#1a0d00" strokeWidth="1.5" strokeLinecap="round"/>
          </g>
        </g>

        {/* === GLASSES === */}
        <g fill="none" stroke="#2a2a35" strokeWidth="1.2" opacity="0.85">
          <ellipse cx={W*0.435} cy={H*0.4} rx={W*0.05} ry={H*0.03}/>
          <ellipse cx={W*0.565} cy={H*0.4} rx={W*0.05} ry={H*0.03}/>
          <line x1={W*0.485} y1={H*0.4} x2={W*0.515} y2={H*0.4}/>
          {/* Lens reflection */}
          <line x1={W*0.41} y1={H*0.385} x2={W*0.43} y2={H*0.395} stroke="#fff" strokeWidth="1.2" opacity="0.5"/>
          <line x1={W*0.54} y1={H*0.385} x2={W*0.56} y2={H*0.395} stroke="#fff" strokeWidth="1.2" opacity="0.5"/>
        </g>

        {/* === BROW === */}
        <g stroke="#7a2548" strokeWidth="2.2" strokeLinecap="round" fill="none">
          <path d={`M ${W*0.4} ${H*(0.36 + e.brow*0.01)} Q ${W*0.435} ${H*(0.355 + e.brow*0.005)} ${W*0.47} ${H*(0.36 + e.brow*0.01)}`}/>
          <path d={`M ${W*0.53} ${H*(0.36 + e.brow*0.01)} Q ${W*0.565} ${H*(0.355 + e.brow*0.005)} ${W*0.6} ${H*(0.36 + e.brow*0.01)}`}/>
        </g>

        {/* === FOREHEAD MARK (third-eye dot — mystic researcher) === */}
        <circle cx={W*0.5} cy={H*0.32} r={W*0.012} fill="#ff3a5a" stroke="#fff" strokeWidth="0.8"
          style={{ filter: "drop-shadow(0 0 4px #ff3a5a)" }}/>
        <circle cx={W*0.5} cy={H*0.32} r={W*0.005} fill="#fff" opacity="0.8"/>

        {/* === BLUSH === */}
        <ellipse cx={W*0.42} cy={H*0.48} rx={W*0.025} ry={H*0.012} fill="url(#kk-blush)"/>
        <ellipse cx={W*0.58} cy={H*0.48} rx={W*0.025} ry={H*0.012} fill="url(#kk-blush)"/>

        {/* === NOSE === */}
        <path d={`M ${W*0.5} ${H*0.46} L ${W*0.495} ${H*0.51}`} stroke="#d4a48a" strokeWidth="1" strokeLinecap="round" fill="none" opacity="0.6"/>

        {/* === MOUTH === */}
        <path d={mouthPath} fill={e.mouth === "open" ? "#c45a78" : "none"} stroke="#a83568" strokeWidth="1.5" strokeLinecap="round"/>
      </g>

      {/* Floating ambient particles around her */}
      {Array.from({length: 8}).map((_, i) => {
        const ang = (t * 0.02 + i * Math.PI / 4);
        const r = W * 0.45;
        const x = W/2 + Math.cos(ang) * r;
        const y = H * 0.5 + Math.sin(ang) * r * 0.5;
        return (
          <circle key={i} cx={x} cy={y} r={1.5} fill="#ffb020" opacity={0.4 + Math.sin(t*0.03 + i)*0.3}
            style={{ filter: "drop-shadow(0 0 4px #ffb020)" }}/>
        );
      })}
    </svg>
  );
}

// =================== ANATOMICAL CYBER HEART ===================
// Realistic heart silhouette with HUD circuitry overlay
function CyberHeart({ bpm = 131, size = 360 }) {
  const [t, setT] = aS(0);
  aE(() => { const id = setInterval(() => setT(x => x + 1), 30); return () => clearInterval(id); }, []);

  const beatMs = 60000 / bpm;
  const phase = ((t * 30) % beatMs) / beatMs;
  const envelope = phase < 0.12
    ? Math.sin((phase / 0.12) * Math.PI)
    : phase < 0.25
      ? Math.sin(((phase - 0.12) / 0.13) * Math.PI) * 0.4
      : 0;
  const scale = 1 + envelope * 0.05;
  const glow = 0.5 + envelope * 0.5;

  const W = size * 1.6, H = size;

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width="100%" height={H} preserveAspectRatio="xMidYMid meet" style={{ display: "block" }}>
      <defs>
        {/* Background atmosphere */}
        <radialGradient id="ch-bg" cx="60%" cy="50%">
          <stop offset="0%" stopColor="#1a2540" stopOpacity="1"/>
          <stop offset="100%" stopColor="#07090d" stopOpacity="1"/>
        </radialGradient>
        {/* Heart body — deep blue/purple base with red tones */}
        <radialGradient id="ch-heart-body" cx="40%" cy="35%">
          <stop offset="0%" stopColor="#5a8acc"/>
          <stop offset="40%" stopColor="#2a3f80"/>
          <stop offset="80%" stopColor="#1a1f4a"/>
          <stop offset="100%" stopColor="#0a0d28"/>
        </radialGradient>
        {/* Red veins / arteries */}
        <linearGradient id="ch-artery" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#ff5060"/>
          <stop offset="100%" stopColor="#a02030"/>
        </linearGradient>
        {/* Glow nodes */}
        <radialGradient id="ch-node-red">
          <stop offset="0%" stopColor="#ff8090"/>
          <stop offset="50%" stopColor="#ff3040"/>
          <stop offset="100%" stopColor="#ff3040" stopOpacity="0"/>
        </radialGradient>
        <radialGradient id="ch-node-amber">
          <stop offset="0%" stopColor="#ffd070"/>
          <stop offset="50%" stopColor="#ffb020"/>
          <stop offset="100%" stopColor="#ffb020" stopOpacity="0"/>
        </radialGradient>
        <radialGradient id="ch-node-cyan">
          <stop offset="0%" stopColor="#80e0ff"/>
          <stop offset="50%" stopColor="#40b0e0"/>
          <stop offset="100%" stopColor="#40b0e0" stopOpacity="0"/>
        </radialGradient>
        <filter id="ch-glow"><feGaussianBlur stdDeviation="2"/></filter>
        <filter id="ch-blur-soft"><feGaussianBlur stdDeviation="0.6"/></filter>
      </defs>

      {/* Background */}
      <rect width={W} height={H} fill="url(#ch-bg)"/>

      {/* === LEFT SIDE: HUD CIRCUITRY DIAGRAM === */}
      <g opacity="0.85">
        {/* Concentric scan rings */}
        {[60, 90, 120, 150].map((r, i) => (
          <circle key={i} cx={W*0.28} cy={H*0.5} r={r}
            fill="none"
            stroke={i === 0 ? "#ff4060" : i === 2 ? "#ffb020" : "#4080c0"}
            strokeWidth={i === 0 ? 2 : 0.8}
            strokeOpacity={0.3 + (i === 0 ? envelope * 0.5 : 0)}
            strokeDasharray={i === 1 ? "3 6" : i === 3 ? "10 4" : "0"}
            transform={`rotate(${t * (i % 2 ? -0.3 : 0.5)} ${W*0.28} ${H*0.5})`}
            style={{ filter: i === 0 ? `drop-shadow(0 0 ${4 + envelope*8}px #ff4060)` : "none" }}
          />
        ))}

        {/* Central mini-heart in HUD */}
        <g transform={`translate(${W*0.28}, ${H*0.5})`} style={{ transformBox: "fill-box" }}>
          <g style={{ transform: `scale(${scale})`, transformOrigin: "center", transition: "transform 30ms linear" }}>
            <path d={`M 0 -25
                      C -25 -45, -55 -35, -55 -10
                      C -55 15, -25 35, 0 60
                      C 25 35, 55 15, 55 -10
                      C 55 -35, 25 -45, 0 -25 Z`}
              fill="url(#ch-node-red)"
              stroke="#ff4060" strokeWidth="1.5"
              style={{ filter: `drop-shadow(0 0 ${10 * glow}px #ff4060)` }}/>
            <path d={`M 0 -25
                      C -25 -45, -55 -35, -55 -10
                      C -55 15, -25 35, 0 60
                      C 25 35, 55 15, 55 -10
                      C 55 -35, 25 -45, 0 -25 Z`}
              fill="none" stroke="#ff8090" strokeWidth="0.6" opacity="0.6"/>
          </g>
        </g>

        {/* Crosshair on HUD heart */}
        <g stroke="#ffb020" strokeWidth="0.8" fill="none" opacity="0.6">
          <line x1={W*0.28 - 70} y1={H*0.5} x2={W*0.28 - 60} y2={H*0.5}/>
          <line x1={W*0.28 + 60} y1={H*0.5} x2={W*0.28 + 70} y2={H*0.5}/>
          <line x1={W*0.28} y1={H*0.5 - 70} x2={W*0.28} y2={H*0.5 - 60}/>
          <line x1={W*0.28} y1={H*0.5 + 60} x2={W*0.28} y2={H*0.5 + 70}/>
        </g>

        {/* Connection lines from HUD to anatomical heart */}
        <g stroke="#4080c0" strokeWidth="0.8" fill="none" opacity="0.7">
          <path d={`M ${W*0.4} ${H*0.4} L ${W*0.46} ${H*0.4} L ${W*0.5} ${H*0.36}`}/>
          <path d={`M ${W*0.4} ${H*0.5} L ${W*0.48} ${H*0.5}`}/>
          <path d={`M ${W*0.4} ${H*0.6} L ${W*0.46} ${H*0.6} L ${W*0.5} ${H*0.64}`}/>
          {/* Connection nodes */}
          <circle cx={W*0.46} cy={H*0.4} r="2" fill="#ffb020" stroke="none"/>
          <circle cx={W*0.48} cy={H*0.5} r="2" fill="#ff4060" stroke="none"/>
          <circle cx={W*0.46} cy={H*0.6} r="2" fill="#4080c0" stroke="none"/>
        </g>

        {/* === LEFT SIDE TELEMETRY TEXT === */}
        <g fontFamily="JetBrains Mono" fontSize="8" fill="#80a0d0" opacity="0.8">
          <text x={W*0.04} y={H*0.18} fill="#ffb020" fontSize="10" fontWeight="600">CARDIAC.SYS</text>
          <text x={W*0.04} y={H*0.21}>STATUS · MONITORING</text>
          <line x1={W*0.04} y1={H*0.225} x2={W*0.22} y2={H*0.225} stroke="#4080c0" strokeWidth="0.5" opacity="0.5"/>
          <text x={W*0.04} y={H*0.245} fill="#7fd49a">▸ rhythm: sinus</text>
          <text x={W*0.04} y={H*0.265} fill="#e26565">▸ rate: tachy +57</text>
          <text x={W*0.04} y={H*0.285}>▸ pre-load: 88%</text>
          <text x={W*0.04} y={H*0.305}>▸ after-load: nominal</text>

          <text x={W*0.04} y={H*0.78} fill="#ffb020" fontSize="10" fontWeight="600">VITAL.LOG</text>
          <line x1={W*0.04} y1={H*0.795} x2={W*0.22} y2={H*0.795} stroke="#4080c0" strokeWidth="0.5" opacity="0.5"/>
          <text x={W*0.04} y={H*0.815}>20:32:18 · {bpm} bpm</text>
          <text x={W*0.04} y={H*0.835}>20:32:17 · {bpm-1} bpm</text>
          <text x={W*0.04} y={H*0.855}>20:32:16 · {bpm} bpm</text>
          <text x={W*0.04} y={H*0.875}>20:32:15 · {bpm+1} bpm</text>
          <text x={W*0.04} y={H*0.895} fill="#e26565">∆ Δ baseline +57</text>
        </g>

        {/* Mini sparkline */}
        <g transform={`translate(${W*0.04}, ${H*0.34})`}>
          <rect width={W*0.2} height={H*0.06} fill="rgba(64,128,192,0.05)" stroke="#4080c0" strokeWidth="0.5" strokeOpacity="0.4"/>
          <polyline points={Array.from({length:30},(_,i) => `${(i/29)*W*0.2},${H*0.06*0.5 + Math.sin(i + t*0.1)*8 - (i===15?20:0)}`).join(" ")}
            fill="none" stroke="#ff4060" strokeWidth="1" opacity="0.8"/>
        </g>
      </g>

      {/* === CENTER/RIGHT: ANATOMICAL HEART === */}
      <g transform={`translate(${W*0.62}, ${H*0.5})`}>
        <g style={{ transformOrigin: "center", transform: `scale(${scale})`, transition: "transform 30ms linear" }}>

          {/* Aortic arch (top) */}
          <path d={`
            M -30 -130
            C -25 -160, 30 -170, 60 -150
            C 80 -135, 75 -110, 50 -100
          `} fill="none" stroke="url(#ch-heart-body)" strokeWidth="22" strokeLinecap="round"/>
          <path d={`
            M -30 -130
            C -25 -160, 30 -170, 60 -150
            C 80 -135, 75 -110, 50 -100
          `} fill="none" stroke="#5a8acc" strokeWidth="20" strokeLinecap="round" opacity="0.4"/>

          {/* Pulmonary trunk */}
          <path d={`M -10 -120 L -25 -80`} stroke="url(#ch-heart-body)" strokeWidth="18" fill="none" strokeLinecap="round"/>

          {/* Vena cava */}
          <path d={`M 70 -130 L 90 -100`} stroke="url(#ch-heart-body)" strokeWidth="14" fill="none" strokeLinecap="round"/>

          {/* === MAIN HEART BODY === */}
          <path d={`
            M -90 -90
            C -110 -70, -115 -30, -100 10
            C -85 50, -50 90, 0 130
            C 50 95, 90 60, 105 20
            C 115 -20, 100 -70, 70 -100
            C 50 -110, 20 -110, 0 -90
            C -20 -110, -50 -110, -90 -90
            Z
          `} fill="url(#ch-heart-body)" stroke="#1a1f4a" strokeWidth="1.5"
            style={{ filter: `drop-shadow(0 0 ${20 * glow}px rgba(255,80,96,0.4))` }}/>

          {/* Highlight on left ventricle */}
          <path d={`
            M -70 -50
            C -85 -20, -85 20, -70 50
            C -55 30, -50 0, -55 -30
            C -60 -45, -65 -50, -70 -50
            Z
          `} fill="#5a8acc" opacity="0.3"/>

          {/* Right ventricle highlight */}
          <ellipse cx="40" cy="20" rx="35" ry="50" fill="#7090d0" opacity="0.2"/>

          {/* === CORONARY ARTERIES (red branching network) === */}
          <g fill="none" stroke="url(#ch-artery)" strokeLinecap="round"
             style={{ filter: `drop-shadow(0 0 ${4 + envelope*8}px #ff4060)` }}>
            {/* Left coronary main */}
            <path d="M -10 -70 C -20 -40, -40 -10, -50 30" strokeWidth="3"/>
            {/* Left branches */}
            <path d="M -30 -20 C -45 -10, -55 5, -65 25" strokeWidth="1.8"/>
            <path d="M -35 0 C -50 10, -55 30, -50 55" strokeWidth="1.8"/>
            <path d="M -42 15 L -55 30" strokeWidth="1.2"/>
            <path d="M -45 35 L -52 55" strokeWidth="1.2"/>

            {/* Right coronary main */}
            <path d="M 10 -70 C 25 -40, 50 -10, 65 30" strokeWidth="3"/>
            {/* Right branches */}
            <path d="M 30 -30 C 50 -15, 65 5, 75 25" strokeWidth="1.8"/>
            <path d="M 40 -10 C 60 5, 70 25, 70 50" strokeWidth="1.8"/>
            <path d="M 50 10 L 65 25" strokeWidth="1.2"/>
            <path d="M 55 30 L 65 50" strokeWidth="1.2"/>

            {/* Apex branches */}
            <path d="M -20 60 C -10 80, 0 95, 10 110" strokeWidth="1.5"/>
            <path d="M 20 60 C 25 80, 20 100, 5 115" strokeWidth="1.5"/>
          </g>

          {/* === GLOWING NODES on heart === */}
          {[
            { x: -10, y: -70, color: "amber", r: 6 },  // sinoatrial
            { x: -30, y: -20, color: "red", r: 5 },
            { x: 30, y: -30, color: "red", r: 5 },
            { x: 0, y: 30, color: "red", r: 7 },        // av node
            { x: 50, y: 30, color: "amber", r: 4 },
            { x: -50, y: 30, color: "amber", r: 4 },
          ].map((n, i) => (
            <g key={i}>
              <circle cx={n.x} cy={n.y} r={n.r * 2} fill={`url(#ch-node-${n.color})`} opacity={0.6 + envelope * 0.4}/>
              <circle cx={n.x} cy={n.y} r={n.r * 0.5} fill="#fff" opacity={0.8 + envelope * 0.2}/>
            </g>
          ))}

          {/* Pulse ring expanding from center on beat */}
          {envelope > 0.1 && (
            <circle cx="0" cy="0" r={50 + envelope * 80} fill="none" stroke="#ff4060"
              strokeWidth={2 - envelope * 1.5} opacity={envelope * 0.6}/>
          )}
        </g>

        {/* === TECH OVERLAY ON HEART === */}
        {/* Circuit traces extending from heart to right edge */}
        <g stroke="#4080c0" strokeWidth="0.8" fill="none" opacity="0.7">
          <path d="M 110 -50 L 150 -50 L 170 -70 L 220 -70"/>
          <path d="M 110 0 L 160 0 L 175 -15 L 230 -15"/>
          <path d="M 110 50 L 150 50 L 170 70 L 220 70"/>
          <path d="M 90 100 L 130 130 L 200 130"/>

          {/* Connection nodes */}
          <circle cx="150" cy="-50" r="2" fill="#ffb020" stroke="none"/>
          <circle cx="220" cy="-70" r="3" fill="#4080c0" stroke="none"/>
          <circle cx="160" cy="0" r="2" fill="#ff4060" stroke="none"/>
          <circle cx="230" cy="-15" r="3" fill="#4080c0" stroke="none"/>
          <circle cx="150" cy="50" r="2" fill="#ffb020" stroke="none"/>
          <circle cx="220" cy="70" r="3" fill="#4080c0" stroke="none"/>
          <circle cx="200" cy="130" r="3" fill="#80e0ff" stroke="none" style={{filter:"drop-shadow(0 0 4px #80e0ff)"}}/>
        </g>

        {/* Right side data labels */}
        <g fontFamily="JetBrains Mono" fontSize="8" fill="#80a0d0" opacity="0.8">
          <text x="240" y="-65">SA · 80mV</text>
          <text x="240" y="-10">AV · 65mV</text>
          <text x="240" y="75">LV · ↑↑</text>
          <text x="220" y="145" fill="#ffb020">APEX OK</text>
        </g>
      </g>

      {/* Top-right BPM display */}
      <g fontFamily="JetBrains Mono">
        <text x={W - 20} y={H * 0.12} textAnchor="end" fontSize="44" fontWeight="700" fill="#ff4060"
          style={{ filter: `drop-shadow(0 0 ${8 + envelope*8}px #ff4060)` }}>
          {bpm}
        </text>
        <text x={W - 20} y={H * 0.16} textAnchor="end" fontSize="11" fill="#80a0d0" letterSpacing="0.2em">BPM · LIVE</text>
        <text x={W - 20} y={H * 0.19} textAnchor="end" fontSize="9" fill="#e26565">▲ TACHY +57</text>
      </g>

      {/* Corner brackets */}
      <g stroke="#ffb020" strokeWidth="1" fill="none" opacity="0.5">
        <path d={`M 16 32 V 16 H 32`}/>
        <path d={`M ${W-32} 16 H ${W-16} V 32`}/>
        <path d={`M 16 ${H-32} V ${H-16} H 32`}/>
        <path d={`M ${W-32} ${H-16} H ${W-16} V ${H-32}`}/>
      </g>

      {/* Top label */}
      <g fontFamily="JetBrains Mono" fontSize="9" fill="#ffb020" opacity="0.7">
        <text x={W*0.5} y={H*0.06} textAnchor="middle" letterSpacing="0.3em">CARDIAC · NEURAL · INTERFACE</text>
      </g>
    </svg>
  );
}

// =================== AVATAR PANEL (sidebar widget) ===================
function AvatarPanel({ emotion = "concerned" }) {
  return (
    <div className="card" style={{ padding: 0, overflow: "hidden", position: "relative" }}>
      <div style={{
        background: "radial-gradient(circle at 50% 30%, rgba(255,143,184,0.1), transparent 70%)",
        padding: "14px 14px 16px",
      }}>
        <div className="card-head" style={{ marginBottom: 4 }}>
          <div className="card-title">KOKONOE <span className="n">// ASI</span></div>
          <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--amber)" }}>● {emotion}</div>
        </div>
        <div style={{ display: "flex", justifyContent: "center" }}>
          <KokonoeAvatar emotion={emotion} size={220}/>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 6, marginTop: 6 }}>
          <Stat label="EMPATHY" value="0.84"/>
          <Stat label="ATTENTION" value="0.92"/>
          <Stat label="DRIFT" value="0.07"/>
        </div>
      </div>
    </div>
  );
}
function Stat({ label, value }) {
  return (
    <div style={{
      background: "rgba(0,0,0,0.3)",
      border: "1px solid var(--line-1)",
      borderRadius: 4,
      padding: "5px 7px",
      fontFamily: "JetBrains Mono",
    }}>
      <div style={{ fontSize: 8, color: "var(--fg-3)", letterSpacing: "0.1em" }}>{label}</div>
      <div style={{ fontSize: 13, color: "var(--amber-hi)", fontWeight: 500 }}>{value}</div>
    </div>
  );
}

window.KokonoeAvatar = KokonoeAvatar;
window.CyberHeart = CyberHeart;
window.AvatarPanel = AvatarPanel;
window.AVATAR_EMOTIONS = AVATAR_EMOTIONS;
