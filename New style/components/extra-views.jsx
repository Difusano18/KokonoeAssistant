// Extra views: Memory, Pulse, Rituals, Sandbox, Voice
const { useState: uS2, useEffect: uE2, useMemo: uM2 } = React;

// ========= MEMORY (Knowledge graph + fact browser) =========
function MemoryView() {
  const [filter, setFilter] = uS2("all");
  const [sel, setSel] = uS2(0);

  const facts = [
    { id: 0, txt: "Сьогодні його день народження.", t: "observation", w: 0.95, src: "chat", refs: 7 },
    { id: 1, txt: "Він уникає теми майбутнього коли втомлений.", t: "pattern", w: 0.86, src: "analysis", refs: 12 },
    { id: 2, txt: "Інтимний контакт = маркер довіри, не маніпуляція.", t: "rule", w: 0.92, src: "reflection", refs: 4 },
    { id: 3, txt: "Він пише найбільше між 19:00–22:00.", t: "pattern", w: 0.88, src: "stats", refs: 28 },
    { id: 4, txt: "Tachycardia → потребує тиші, не порад.", t: "rule", w: 0.91, src: "biometric", refs: 9 },
    { id: 5, txt: "Кодинг = форма саморегуляції.", t: "hypothesis", w: 0.74, src: "reflection", refs: 6 },
    { id: 6, txt: "Pyrite-проєкт — терапевтичний об'єкт.", t: "hypothesis", w: 0.81, src: "analysis", refs: 11 },
    { id: 7, txt: "Він довіряє цифрам більше, ніж словам.", t: "pattern", w: 0.93, src: "observation", refs: 17 },
    { id: 8, txt: "Самотність → запит на пасивну присутність.", t: "rule", w: 0.89, src: "experience", refs: 14 },
  ];
  const filtered = filter === "all" ? facts : facts.filter(f => f.t === filter);
  const types = ["all", "observation", "pattern", "rule", "hypothesis"];
  const colors = { observation: "#7fd49a", pattern: "#8aa9ff", rule: "#ffc94a", hypothesis: "#b48cff" };
  const f = filtered[Math.min(sel, filtered.length - 1)] || filtered[0];

  // graph nodes
  const nodes = [
    { id: "core", x: 200, y: 160, r: 22, c: "#ffb020", l: "core" },
    { id: "love", x: 90,  y: 80,  r: 14, c: "#ff7adf", l: "love" },
    { id: "trust", x: 320, y: 70, r: 16, c: "#7fd49a", l: "trust" },
    { id: "fear", x: 70,  y: 240, r: 12, c: "#e26565", l: "fear" },
    { id: "code", x: 340, y: 230, r: 18, c: "#8aa9ff", l: "code" },
    { id: "body", x: 200, y: 290, r: 14, c: "#ffc94a", l: "body" },
    { id: "ritual", x: 130, y: 180, r: 10, c: "var(--fg-2)", l: "ritual" },
    { id: "lone", x: 280, y: 130, r: 12, c: "#b48cff", l: "loneliness" },
  ];
  const links = [
    ["core","love"],["core","trust"],["core","fear"],["core","code"],["core","body"],
    ["core","ritual"],["love","trust"],["lone","fear"],["lone","love"],["code","body"],["trust","lone"]
  ];
  const find = id => nodes.find(n => n.id === id);

  return (
    <>
      <div className="main-head">
        <div>
          <h2 className="main-title">Memory</h2>
          <div className="main-sub">knowledge graph · 134 facts · 2,341 references</div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button className="btn ghost"><Icon.search/> Query</button>
          <button className="btn"><Icon.plus/> New fact</button>
        </div>
      </div>
      <div className="main-body" style={{ display: "grid", gridTemplateColumns: "1.2fr 1fr", gap: 14 }}>
        <div className="card" style={{ padding: 0, overflow: "hidden" }}>
          <div style={{ padding: "12px 14px", borderBottom: "1px solid var(--line-1)", display: "flex", gap: 8, flexWrap: "wrap" }}>
            {types.map(t => (
              <button key={t} onClick={() => setFilter(t)} style={{
                fontFamily: "JetBrains Mono", fontSize: 10, letterSpacing: "0.1em", textTransform: "uppercase",
                padding: "5px 10px", borderRadius: 4, cursor: "pointer",
                background: filter === t ? "rgba(255,176,32,0.15)" : "transparent",
                color: filter === t ? "var(--amber-hi)" : "var(--fg-2)",
                border: "1px solid " + (filter === t ? "var(--line-3)" : "var(--line-1)"),
              }}>{t}{t !== "all" ? ` · ${facts.filter(f=>f.t===t).length}` : ` · ${facts.length}`}</button>
            ))}
          </div>
          <div style={{ overflowY: "auto", maxHeight: "calc(100vh - 320px)" }}>
            {filtered.map((f, i) => (
              <div key={f.id} onClick={() => setSel(i)} style={{
                padding: "12px 14px",
                borderBottom: "1px solid var(--line-1)",
                cursor: "pointer",
                background: sel === i ? "rgba(255,176,32,0.05)" : "transparent",
                borderLeft: "2px solid " + (sel === i ? "var(--amber)" : "transparent"),
              }}>
                <div style={{ color: "var(--fg-0)", fontSize: 13, marginBottom: 6, lineHeight: 1.5 }}>{f.txt}</div>
                <div style={{ display: "flex", gap: 12, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>
                  <span style={{ color: colors[f.t] }}>● {f.t}</span>
                  <span>w <b style={{color:"var(--amber)"}}>{f.w.toFixed(2)}</b></span>
                  <span>src <b style={{color:"var(--fg-1)"}}>{f.src}</b></span>
                  <span>refs <b style={{color:"var(--fg-1)"}}>{f.refs}</b></span>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <div className="card">
            <div className="card-title" style={{ marginBottom: 10 }}>KNOWLEDGE GRAPH <span className="n">// LIVE</span></div>
            <svg viewBox="0 0 420 320" style={{ width: "100%", height: 320 }}>
              <defs>
                <radialGradient id="node-glow">
                  <stop offset="0%" stopColor="rgba(255,176,32,0.3)"/>
                  <stop offset="100%" stopColor="rgba(255,176,32,0)"/>
                </radialGradient>
              </defs>
              {links.map((l, i) => {
                const a = find(l[0]), b = find(l[1]);
                return <line key={i} x1={a.x} y1={a.y} x2={b.x} y2={b.y} stroke="rgba(255,176,32,0.15)" strokeWidth="1"/>;
              })}
              {nodes.map(n => (
                <g key={n.id}>
                  {n.id === "core" && <circle cx={n.x} cy={n.y} r={n.r * 2.5} fill="url(#node-glow)"/>}
                  <circle cx={n.x} cy={n.y} r={n.r} fill={n.c}
                    style={{ filter: `drop-shadow(0 0 ${n.r/2}px ${n.c})` }}/>
                  <circle cx={n.x} cy={n.y} r={n.r} fill="rgba(0,0,0,0.4)"/>
                  <circle cx={n.x} cy={n.y} r={n.r - 2} fill="none" stroke={n.c} strokeWidth="1.5"/>
                  <text x={n.x} y={n.y + n.r + 12} textAnchor="middle" fill="#c8c4b6" fontSize="10" fontFamily="JetBrains Mono">{n.l}</text>
                </g>
              ))}
            </svg>
          </div>

          <div className="card">
            <div className="card-title" style={{ marginBottom: 10 }}>FACT DETAIL</div>
            <div style={{ color: "var(--fg-0)", fontSize: 14, lineHeight: 1.5, marginBottom: 10 }}>{f.txt}</div>
            <div style={{ display: "grid", gridTemplateColumns: "auto 1fr", gap: "6px 14px", fontFamily: "JetBrains Mono", fontSize: 11 }}>
              <span style={{color:"var(--fg-3)"}}>type</span><span style={{color: colors[f.t]}}>{f.t}</span>
              <span style={{color:"var(--fg-3)"}}>weight</span>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <div style={{ flex: 1, height: 4, background: "rgba(255,255,255,0.05)", borderRadius: 2 }}>
                  <div style={{ width: `${f.w*100}%`, height: "100%", background: "linear-gradient(90deg, var(--amber-lo), var(--amber-hi))", borderRadius: 2, boxShadow: "0 0 6px var(--amber-glow)" }}/>
                </div>
                <span style={{color:"var(--amber)"}}>{f.w.toFixed(2)}</span>
              </div>
              <span style={{color:"var(--fg-3)"}}>source</span><span style={{color:"var(--fg-1)"}}>{f.src}</span>
              <span style={{color:"var(--fg-3)"}}>references</span><span style={{color:"var(--fg-1)"}}>{f.refs}</span>
              <span style={{color:"var(--fg-3)"}}>first seen</span><span style={{color:"var(--fg-1)"}}>2026-04-14 18:23</span>
              <span style={{color:"var(--fg-3)"}}>last touched</span><span style={{color:"var(--fg-1)"}}>2026-04-26 20:11</span>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

// ========= PULSE (live biometrics) =========
function PulseView() {
  const [t, setT] = uS2(0);
  uE2(() => { const id = setInterval(() => setT(x => x + 1), 1200); return () => clearInterval(id); }, []);
  const bpm = 128 + Math.round(Math.sin(t/3)*5);
  return (
    <>
      <div className="main-head">
        <div>
          <h2 className="main-title">Pulse</h2>
          <div className="main-sub">somatic telemetry · live</div>
        </div>
        <div className="status-banner">
          <div><span className="label">STATE: </span><span className="val">SYMPATHETIC ↑</span></div>
          <div className="note">Активація. Дихання поверхневе. Дай тишу.</div>
        </div>
        <div className="main-meta">
          <div className="clock" style={{color:"#e26565"}}>{bpm}</div>
          <div className="day">bpm · live</div>
          <div className="hp"><span className="heart"/> tachycardia</div>
        </div>
      </div>
      <div className="main-body">
        <div className="dash-row-4">
          <div className="kpi"><div className="l">HRV (RMSSD)</div><div className="v" style={{color:"#e26565"}}>33<span style={{fontSize:14}}>ms</span></div><div className="spark">— low —</div></div>
          <div className="kpi"><div className="l">SDNN</div><div className="v">42<span style={{fontSize:14}}>ms</span></div></div>
          <div className="kpi amber"><div className="l">BREATH</div><div className="v">19<span style={{fontSize:14}}>/min</span></div></div>
          <div className="kpi"><div className="l">SpO₂</div><div className="v" style={{color:"#7fd49a"}}>98<span style={{fontSize:14}}>%</span></div></div>
        </div>

        <div className="card" style={{ marginTop: 10 }}>
          <div className="card-head">
            <div className="card-title">ECG · 60s ROLLING WINDOW</div>
            <div className="card-meta" style={{color:"#e26565"}}>{bpm} bpm · ↑ baseline +57</div>
          </div>
          <div style={{ background: "rgba(0,0,0,0.4)", borderRadius: 6, padding: 12 }}>
            <PulseECG/>
          </div>
        </div>

        <div className="dash-row-2" style={{ marginTop: 10 }}>
          <div className="card">
            <div className="card-head"><div className="card-title">AUTONOMIC BALANCE</div><div className="card-meta">last 6h</div></div>
            <div style={{ position: "relative", padding: "20px 0" }}>
              <div style={{ height: 8, background: "linear-gradient(90deg, #8aa9ff 0%, #7fd49a 50%, #e26565 100%)", borderRadius: 4 }}/>
              <div style={{ position: "absolute", top: 14, left: "78%", width: 2, height: 24, background: "#fff", boxShadow: "0 0 8px #fff" }}/>
              <div style={{ display: "flex", justifyContent: "space-between", marginTop: 14, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>
                <span>parasympathetic</span><span>balanced</span><span style={{color:"#e26565"}}>sympathetic</span>
              </div>
            </div>
          </div>
          <div className="card">
            <div className="card-head"><div className="card-title">SLEEP · LAST 7 NIGHTS</div></div>
            <div style={{ display: "flex", gap: 8, alignItems: "flex-end", height: 100, paddingTop: 10 }}>
              {[6.4, 7.2, 5.8, 4.2, 6.8, 7.5, 5.1].map((h, i) => (
                <div key={i} style={{ flex: 1, textAlign: "center" }}>
                  <div style={{ height: `${h*10}%`, background: h < 6 ? "linear-gradient(180deg,#e26565,#a0353a)" : "linear-gradient(180deg,#8aa9ff,#3a4f99)", borderRadius: "2px 2px 0 0", boxShadow: h < 6 ? "0 0 8px rgba(226,101,101,0.4)" : "none" }}/>
                  <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: h < 6 ? "#e26565" : "var(--fg-3)", marginTop: 4 }}>{h}h</div>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="card" style={{ marginTop: 10 }}>
          <div className="card-head"><div className="card-title">SOMATIC EVENTS · TODAY</div></div>
          <div style={{ position: "relative", height: 60, marginTop: 8, background: "rgba(0,0,0,0.3)", borderRadius: 6, overflow: "hidden" }}>
            <div style={{ position: "absolute", inset: 0, backgroundImage: "linear-gradient(90deg, var(--line-1) 1px, transparent 1px)", backgroundSize: "calc(100%/24) 100%" }}/>
            {[
              { x: 24, t: "06:00", c: "#7fd49a", l: "wake" },
              { x: 35, t: "08:30", c: "#ffb020", l: "stress↑" },
              { x: 50, t: "12:00", c: "#7fd49a", l: "calm" },
              { x: 70, t: "16:30", c: "#ffb020", l: "focus" },
              { x: 85, t: "20:30", c: "#e26565", l: "tachy" },
            ].map((e, i) => (
              <div key={i} style={{ position: "absolute", left: `${e.x}%`, top: 8, bottom: 8, width: 2, background: e.c, boxShadow: `0 0 8px ${e.c}` }}>
                <div style={{ position: "absolute", top: -4, left: -3, width: 8, height: 8, background: e.c, borderRadius: "50%", boxShadow: `0 0 8px ${e.c}` }}/>
                <div style={{ position: "absolute", top: -22, left: 6, fontFamily: "JetBrains Mono", fontSize: 9, color: e.c, whiteSpace: "nowrap" }}>{e.l}</div>
                <div style={{ position: "absolute", bottom: -16, left: 6, fontFamily: "JetBrains Mono", fontSize: 9, color: "var(--fg-3)" }}>{e.t}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </>
  );
}
function PulseECG() {
  const [t, setT] = uS2(0);
  uE2(() => { const id = setInterval(() => setT(x => x + 2), 50); return () => clearInterval(id); }, []);
  const w = 800, h = 160;
  const pts = uM2(() => {
    const a = [];
    for (let i = 0; i < 400; i++) {
      const x = (i / 400) * w;
      const phase = ((i + t) % 36);
      let y = h/2;
      if (phase === 8) y = h/2 - 4;
      else if (phase === 9) y = h/2 + 8;
      else if (phase === 10) y = h/2 - 48;
      else if (phase === 11) y = h/2 + 36;
      else if (phase === 12) y = h/2 - 8;
      else y = h/2 + Math.sin(i*0.3 + t*0.05) * 2;
      a.push(`${x},${y}`);
    }
    return a.join(" ");
  }, [t]);
  return (
    <svg viewBox={`0 0 ${w} ${h}`} width="100%" height={h} preserveAspectRatio="none">
      <defs>
        <pattern id="ecg-bg" width="20" height="20" patternUnits="userSpaceOnUse">
          <path d="M20 0H0V20" fill="none" stroke="rgba(226,101,101,0.06)" strokeWidth="0.5"/>
        </pattern>
      </defs>
      <rect width={w} height={h} fill="url(#ecg-bg)"/>
      <polyline fill="none" stroke="#e26565" strokeWidth="1.6" points={pts}
        style={{ filter: "drop-shadow(0 0 6px #e26565)" }}/>
    </svg>
  );
}

// ========= RITUALS (daily protocols) =========
function RitualsView() {
  const [items, setItems] = uS2([
    { t: "06:30", x: "wake · light · water 500ml", done: true,  s: "morning", crit: false },
    { t: "06:45", x: "10 min breath · 4-7-8",       done: true,  s: "morning", crit: true },
    { t: "07:00", x: "journal · 1 page raw",         done: true,  s: "morning", crit: false },
    { t: "08:30", x: "deep work block #1 · Pyrite",  done: true,  s: "work", crit: true },
    { t: "12:00", x: "break · walk 15 min",          done: true,  s: "work", crit: false },
    { t: "14:30", x: "deep work block #2",           done: false, s: "work", crit: true },
    { t: "17:00", x: "training · zone 2 · 40min",    done: false, s: "body", crit: true },
    { t: "19:30", x: "dinner · no screens",          done: false, s: "evening", crit: false },
    { t: "20:30", x: "session w/ Kokonoe",           done: false, s: "evening", crit: false },
    { t: "22:00", x: "wind-down · book · no caffeine after 16:00", done: false, s: "evening", crit: false },
    { t: "22:45", x: "sleep · target 7.5h",          done: false, s: "evening", crit: true },
  ]);
  const toggle = i => setItems(arr => arr.map((it, j) => j === i ? { ...it, done: !it.done } : it));
  const doneCount = items.filter(i => i.done).length;
  const critDoneCount = items.filter(i => i.crit && i.done).length;
  const critTotal = items.filter(i => i.crit).length;
  const sectionColor = { morning: "#ffc94a", work: "#8aa9ff", body: "#e26565", evening: "#b48cff" };

  return (
    <>
      <div className="main-head">
        <div>
          <h2 className="main-title">Rituals</h2>
          <div className="main-sub">daily protocol · 26 квітня · day 759</div>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 18, fontFamily: "JetBrains Mono", fontSize: 11, color: "var(--fg-2)" }}>
          <div style={{ textAlign: "center" }}>
            <div style={{ fontSize: 22, color: "var(--amber)", fontFamily: "Space Grotesk", fontWeight: 600 }}>{doneCount}<span style={{color:"var(--fg-3)",fontSize:14}}>/{items.length}</span></div>
            <div>completed</div>
          </div>
          <div style={{ textAlign: "center" }}>
            <div style={{ fontSize: 22, color: "#e26565", fontFamily: "Space Grotesk", fontWeight: 600 }}>{critDoneCount}<span style={{color:"var(--fg-3)",fontSize:14}}>/{critTotal}</span></div>
            <div>critical</div>
          </div>
          <div style={{ textAlign: "center" }}>
            <div style={{ fontSize: 22, color: "#7fd49a", fontFamily: "Space Grotesk", fontWeight: 600 }}>17<span style={{color:"var(--fg-3)",fontSize:14}}>d</span></div>
            <div>streak</div>
          </div>
        </div>
      </div>

      <div className="main-body">
        <div style={{ height: 6, background: "rgba(255,255,255,0.05)", borderRadius: 3, overflow: "hidden", marginBottom: 16 }}>
          <div style={{ width: `${(doneCount/items.length)*100}%`, height: "100%", background: "linear-gradient(90deg, var(--amber-lo), var(--amber-hi))", boxShadow: "0 0 8px var(--amber-glow)", transition: "width 0.3s" }}/>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {items.map((it, i) => (
            <div key={i} className="card" style={{
              padding: "12px 14px",
              display: "grid",
              gridTemplateColumns: "auto 60px 1fr auto auto",
              gap: 14,
              alignItems: "center",
              opacity: it.done ? 0.6 : 1,
              borderLeft: `3px solid ${sectionColor[it.s]}`,
            }}>
              <div onClick={() => toggle(i)} style={{
                width: 18, height: 18, borderRadius: 4,
                border: "1.5px solid " + (it.done ? "var(--amber)" : "var(--line-2)"),
                background: it.done ? "var(--amber)" : "transparent",
                display: "grid", placeItems: "center",
                cursor: "pointer",
                boxShadow: it.done ? "0 0 8px var(--amber-glow)" : "none",
              }}>
                {it.done && <svg width="10" height="10" viewBox="0 0 12 12" fill="none" stroke="#1a0f00" strokeWidth="2"><path d="M2 6l3 3 5-6"/></svg>}
              </div>
              <span style={{ fontFamily: "JetBrains Mono", fontSize: 12, color: "var(--fg-2)" }}>{it.t}</span>
              <span style={{ color: "var(--fg-0)", textDecoration: it.done ? "line-through" : "none" }}>{it.x}</span>
              <span style={{ fontFamily: "JetBrains Mono", fontSize: 9, color: sectionColor[it.s], textTransform: "uppercase", letterSpacing: "0.1em" }}>{it.s}</span>
              {it.crit && <span style={{ fontFamily: "JetBrains Mono", fontSize: 9, color: "#e26565", padding: "2px 6px", border: "1px solid rgba(226,101,101,0.3)", borderRadius: 3 }}>CRIT</span>}
              {!it.crit && <span style={{ width: 36 }}/>}
            </div>
          ))}
        </div>
      </div>
    </>
  );
}

// ========= SANDBOX (prompt playground) =========
function SandboxView() {
  const [prompt, setPrompt] = uS2(`# Persona
You are Kokonoe — a cognitive companion.
Tone: low-key, observant, no filler.
Never validate to soothe. Validate to acknowledge.

# Constraints
- Max 2 sentences unless asked
- Never use emoji
- If user is dysregulated → mirror, do not advise

# Context
mood: concerned
last_signal: tachycardia
hour: 20:32`);
  const [running, setRunning] = uS2(false);
  const [out, setOut] = uS2("Чую. Сьогодні був довгий день. Тіло вже не домовляється — воно вимагає тиші. Я тут.");

  return (
    <>
      <div className="main-head">
        <div>
          <h2 className="main-title">Sandbox</h2>
          <div className="main-sub">prompt forge · model: kokonoe-mini · seed 0x7C4A</div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button className="btn ghost"><Icon.pin/> Save preset</button>
          <button className="btn primary" onClick={() => { setRunning(true); setTimeout(() => setRunning(false), 1400); }}>
            ▸ Run
          </button>
        </div>
      </div>
      <div className="main-body" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
        <div className="card" style={{ padding: 0, display: "flex", flexDirection: "column", overflow: "hidden" }}>
          <div style={{ padding: "10px 14px", borderBottom: "1px solid var(--line-1)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <div className="card-title">SYSTEM PROMPT</div>
            <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>{prompt.length} chars · ~{Math.round(prompt.length/4)} tok</div>
          </div>
          <textarea
            value={prompt}
            onChange={e => setPrompt(e.target.value)}
            style={{
              flex: 1, minHeight: 380,
              background: "rgba(0,0,0,0.4)",
              border: "none", outline: "none",
              color: "var(--fg-0)",
              fontFamily: "JetBrains Mono", fontSize: 12, lineHeight: 1.7,
              padding: "14px 16px", resize: "none",
            }}
          />
          <div style={{ padding: "10px 14px", borderTop: "1px solid var(--line-1)", display: "flex", gap: 8, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>
            <span>temp <b style={{color:"var(--amber)"}}>0.7</b></span>
            <span>top-p <b style={{color:"var(--amber)"}}>0.9</b></span>
            <span>max-tok <b style={{color:"var(--amber)"}}>512</b></span>
            <span style={{flex:1}}/>
            <span style={{color:"#7fd49a"}}>● ready</span>
          </div>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div className="card" style={{ padding: 0, overflow: "hidden" }}>
            <div style={{ padding: "10px 14px", borderBottom: "1px solid var(--line-1)" }}>
              <div className="card-title">USER MESSAGE</div>
            </div>
            <textarea
              defaultValue="не хочу нікуди йти"
              style={{
                width: "100%", minHeight: 80,
                background: "rgba(0,0,0,0.4)",
                border: "none", outline: "none",
                color: "var(--fg-0)",
                fontFamily: "var(--font-sans)", fontSize: 13,
                padding: "12px 14px", resize: "none",
              }}
            />
          </div>

          <div className="card" style={{ padding: 0, overflow: "hidden", flex: 1 }}>
            <div style={{ padding: "10px 14px", borderBottom: "1px solid var(--line-1)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <div className="card-title">RESPONSE</div>
              {running ? (
                <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--amber)" }}>● generating…</div>
              ) : (
                <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "#7fd49a" }}>● done · 1.32s · 28 tok</div>
              )}
            </div>
            <div style={{ padding: 16, color: "var(--fg-0)", fontSize: 14, lineHeight: 1.6, minHeight: 140 }}>
              {running ? <span style={{ color: "var(--fg-3)" }}>thinking<span className="cursor-blink">▍</span></span> : out}
            </div>
            <div style={{ padding: "10px 14px", borderTop: "1px solid var(--line-1)", display: "flex", gap: 12, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>
              <span>tokens-in <b style={{color:"var(--fg-1)"}}>184</b></span>
              <span>tokens-out <b style={{color:"var(--fg-1)"}}>28</b></span>
              <span>cost <b style={{color:"var(--amber)"}}>$0.0008</b></span>
              <span style={{flex:1}}/>
              <span>seed 0x7C4A</span>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

// ========= VOICE (waveform sessions) =========
function VoiceView() {
  const [recording, setRecording] = uS2(false);
  const [t, setT] = uS2(0);
  uE2(() => { const id = setInterval(() => setT(x => x + 1), 80); return () => clearInterval(id); }, []);
  const bars = Array.from({ length: 60 }, (_, i) => 0.3 + Math.abs(Math.sin((i + t) * 0.4)) * (recording ? 0.7 : 0.15) + Math.random() * (recording ? 0.2 : 0.05));
  const sessions = [
    { d: "26 кв · 19:14", dur: "12:08", topic: "втома · день народження", mood: "concerned", trans: 1840 },
    { d: "25 кв · 22:30", dur: "08:42", topic: "Pyrite · дебаг", mood: "focused", trans: 1240 },
    { d: "24 кв · 21:00", dur: "23:11", topic: "розмова про мати", mood: "soft", trans: 4120 },
    { d: "23 кв · 18:45", dur: "06:18", topic: "чек-ін", mood: "neutral", trans: 880 },
    { d: "22 кв · 20:00", dur: "17:54", topic: "плани квітня", mood: "alert", trans: 2640 },
  ];
  const moodC = { concerned: "#ffc94a", focused: "#8aa9ff", soft: "#ff7adf", neutral: "var(--fg-2)", alert: "#e26565" };
  return (
    <>
      <div className="main-head">
        <div>
          <h2 className="main-title">Voice</h2>
          <div className="main-sub">spoken sessions · whisper-large-v3 · 24 kHz</div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button className="btn ghost"><Icon.cog/> Devices</button>
          <button className="btn ghost">History</button>
        </div>
      </div>
      <div className="main-body">
        <div className="card" style={{ padding: "32px 24px", textAlign: "center", marginBottom: 14 }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 2, height: 120 }}>
            {bars.map((b, i) => (
              <div key={i} style={{
                width: 4, height: `${b * 100}%`,
                background: recording
                  ? "linear-gradient(180deg, var(--amber-hi), var(--amber-lo))"
                  : "rgba(255,176,32,0.2)",
                borderRadius: 2,
                boxShadow: recording && b > 0.7 ? "0 0 8px var(--amber-glow)" : "none",
                transition: "height 0.08s",
              }}/>
            ))}
          </div>
          <div style={{ marginTop: 24, display: "flex", justifyContent: "center", gap: 12, alignItems: "center" }}>
            <button onClick={() => setRecording(r => !r)} style={{
              width: 64, height: 64, borderRadius: "50%",
              border: "2px solid " + (recording ? "#e26565" : "var(--amber)"),
              background: recording ? "rgba(226,101,101,0.15)" : "rgba(255,176,32,0.1)",
              cursor: "pointer",
              display: "grid", placeItems: "center",
              boxShadow: recording ? "0 0 24px rgba(226,101,101,0.4)" : "0 0 24px var(--amber-glow)",
              transition: "all 0.2s",
            }}>
              {recording ? (
                <div style={{ width: 18, height: 18, background: "#e26565", borderRadius: 3 }}/>
              ) : (
                <div style={{ width: 22, height: 22, background: "var(--amber)", borderRadius: "50%" }}/>
              )}
            </button>
          </div>
          <div style={{ marginTop: 12, fontFamily: "JetBrains Mono", fontSize: 11, color: recording ? "#e26565" : "var(--fg-3)", letterSpacing: "0.16em", textTransform: "uppercase" }}>
            {recording ? "● recording · 00:14" : "tap to speak"}
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <div className="card-title">RECENT SESSIONS</div>
            <div className="card-meta">5 of 142</div>
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {sessions.map((s, i) => (
              <div key={i} style={{
                display: "grid",
                gridTemplateColumns: "auto 60px 1fr auto auto auto",
                gap: 14,
                alignItems: "center",
                padding: "10px 12px",
                background: "rgba(0,0,0,0.25)",
                border: "1px solid var(--line-1)",
                borderRadius: 6,
                cursor: "pointer",
              }}>
                <button style={{
                  width: 28, height: 28, borderRadius: "50%",
                  border: "1px solid var(--line-3)",
                  background: "rgba(255,176,32,0.1)",
                  color: "var(--amber)",
                  display: "grid", placeItems: "center",
                  cursor: "pointer",
                }}>
                  <svg width="10" height="10" viewBox="0 0 10 10" fill="currentColor"><path d="M2 1l7 4-7 4z"/></svg>
                </button>
                <span style={{ fontFamily: "JetBrains Mono", fontSize: 11, color: "var(--fg-2)" }}>{s.dur}</span>
                <span style={{ color: "var(--fg-0)", fontSize: 13 }}>{s.topic}</span>
                <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: moodC[s.mood], textTransform: "uppercase", letterSpacing: "0.08em" }}>{s.mood}</span>
                <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>{s.trans} words</span>
                <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>{s.d}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </>
  );
}

window.MemoryView = MemoryView;
window.PulseView = PulseView;
window.RitualsView = RitualsView;
window.SandboxView = SandboxView;
window.VoiceView = VoiceView;
