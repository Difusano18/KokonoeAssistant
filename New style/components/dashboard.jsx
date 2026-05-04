// Dashboard view — Neural / Dev / Memory / System tabs
const { useState, useMemo, useEffect } = React;

// ---------- Reusable visualizations ----------
function BarChart({ data, highlightIdx, axis, height = 140 }) {
  const max = Math.max(...data, 1);
  return (
    <>
      <div className="bars" style={{ height }}>
        {data.map((v, i) => (
          <div
            key={i}
            className={"bar" + (i === highlightIdx ? " amber" : "")}
            style={{ height: `${(v / max) * 100}%` }}
            title={`${v}`}
          />
        ))}
      </div>
      {axis && (
        <div className="bars-axis">
          {axis.map((a, i) => <span key={i}>{a}</span>)}
        </div>
      )}
    </>
  );
}

function Donut({ segments, size = 140, label, sublabel }) {
  const r = size / 2 - 18;
  const C = 2 * Math.PI * r;
  const total = segments.reduce((s, x) => s + x.value, 0);
  let offset = 0;
  return (
    <svg className="donut" viewBox={`0 0 ${size} ${size}`} style={{ width: size, height: size }}>
      <circle cx={size/2} cy={size/2} r={r} fill="none" stroke="rgba(255,255,255,0.04)" strokeWidth="14"/>
      {segments.map((s, i) => {
        const dash = (s.value / total) * C;
        const el = (
          <circle key={i}
            cx={size/2} cy={size/2} r={r} fill="none"
            stroke={s.color} strokeWidth="14"
            strokeDasharray={`${dash} ${C - dash}`}
            strokeDashoffset={-offset}
            transform={`rotate(-90 ${size/2} ${size/2})`}
            style={{ filter: i === 0 ? "drop-shadow(0 0 6px " + s.color + ")" : "none" }}
          />
        );
        offset += dash;
        return el;
      })}
      <text x="50%" y="48%" textAnchor="middle" fill="#f3f0e6" fontSize="20" fontWeight="600" fontFamily="Space Grotesk">{label}</text>
      {sublabel && <text x="50%" y="62%" textAnchor="middle" fill="#8a8678" fontSize="9" fontFamily="JetBrains Mono" letterSpacing="0.1em">{sublabel}</text>}
    </svg>
  );
}

function LineChart({ series, w = 800, h = 160, gridX = 8, gridY = 4 }) {
  const allY = series.flatMap(s => s.points.map(p => p[1]));
  const minY = Math.min(...allY), maxY = Math.max(...allY);
  const allX = series[0].points.map(p => p[0]);
  const minX = Math.min(...allX), maxX = Math.max(...allX);
  const X = x => ((x - minX) / (maxX - minX || 1)) * (w - 40) + 30;
  const Y = y => h - 20 - ((y - minY) / (maxY - minY || 1)) * (h - 30);
  return (
    <svg viewBox={`0 0 ${w} ${h}`} width="100%" height={h} preserveAspectRatio="none">
      {/* grid */}
      {Array.from({ length: gridY + 1 }).map((_, i) => (
        <line key={"h"+i} x1="30" x2={w-10} y1={(i/gridY)*(h-30)+10} y2={(i/gridY)*(h-30)+10} stroke="rgba(255,255,255,0.04)"/>
      ))}
      {Array.from({ length: gridX + 1 }).map((_, i) => (
        <line key={"v"+i} y1="10" y2={h-20} x1={(i/gridX)*(w-40)+30} x2={(i/gridX)*(w-40)+30} stroke="rgba(255,255,255,0.03)"/>
      ))}
      {series.map((s, i) => {
        const d = s.points.map((p, j) => (j ? "L" : "M") + X(p[0]) + " " + Y(p[1])).join(" ");
        return (
          <g key={i}>
            <path d={d + ` L ${X(s.points[s.points.length-1][0])} ${h-20} L ${X(s.points[0][0])} ${h-20} Z`} fill={s.fill || "transparent"} opacity="0.15"/>
            <path d={d} fill="none" stroke={s.color} strokeWidth={s.dash ? 1.4 : 2} strokeDasharray={s.dash || "0"}
              style={{ filter: s.glow ? `drop-shadow(0 0 6px ${s.color})` : "none" }}
            />
          </g>
        );
      })}
    </svg>
  );
}

function Spark({ values, color = "#ffb020", h = 24 }) {
  const max = Math.max(...values), min = Math.min(...values);
  const w = 100;
  const pts = values.map((v, i) => {
    const x = (i / (values.length - 1)) * w;
    const y = h - ((v - min) / (max - min || 1)) * (h - 4) - 2;
    return `${x},${y}`;
  }).join(" ");
  return (
    <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" style={{ width: "100%", height: h }}>
      <polyline fill="none" stroke={color} strokeWidth="1.5" points={pts}
        style={{ filter: `drop-shadow(0 0 4px ${color})` }}/>
    </svg>
  );
}

function HeartRate() {
  const [t, setT] = useState(0);
  useEffect(() => { const id = setInterval(() => setT(x => x + 1), 60); return () => clearInterval(id); }, []);
  // Generate ECG-style trace
  const w = 600, h = 70;
  const pts = useMemo(() => {
    const a = [];
    for (let i = 0; i < 220; i++) {
      const x = (i / 220) * w;
      const phase = ((i + t) % 30);
      let y = h/2;
      if (phase === 8) y = h/2 - 4;
      else if (phase === 9) y = h/2 + 6;
      else if (phase === 10) y = h/2 - 22;
      else if (phase === 11) y = h/2 + 18;
      else if (phase === 12) y = h/2 - 6;
      else if (phase === 13) y = h/2;
      else y = h/2 + (Math.sin(i*0.4) * 1.2);
      a.push(`${x},${y}`);
    }
    return a.join(" ");
  }, [t]);
  return (
    <svg viewBox={`0 0 ${w} ${h}`} width="100%" height={h} preserveAspectRatio="none">
      {/* grid */}
      <defs>
        <pattern id="ecg-grid" width="20" height="14" patternUnits="userSpaceOnUse">
          <path d="M20 0H0V14" fill="none" stroke="rgba(226,101,101,0.08)" strokeWidth="0.5"/>
        </pattern>
      </defs>
      <rect width={w} height={h} fill="url(#ecg-grid)"/>
      <polyline fill="none" stroke="#e26565" strokeWidth="1.5" points={pts}
        style={{ filter: "drop-shadow(0 0 4px #e26565)" }}/>
    </svg>
  );
}

function Heatmap({ rows = 7, cols = 24, data }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: `repeat(${cols}, 1fr)`, gap: 2 }}>
      {Array.from({ length: rows * cols }).map((_, i) => {
        const v = data[i] ?? 0;
        const a = Math.min(0.9, v * 0.9 + 0.05);
        return <div key={i} style={{
          aspectRatio: "1",
          background: `rgba(255, 176, 32, ${a})`,
          borderRadius: 2,
          boxShadow: v > 0.7 ? "0 0 6px rgba(255,176,32,0.4)" : "none",
        }}/>;
      })}
    </div>
  );
}

// ---------- Tab content ----------
function NeuralTab() {
  const hours = [3,7,12,9,14,22,18,28,35,42,38,55,68,82,75,98,162,142,128,115,95,72,48,28];
  const moodPts = Array.from({ length: 80 }, (_, i) => [i, 0.3 + Math.sin(i/6)*0.15 + (i>50? 0.25:0) + Math.random()*0.04]);
  const targetPts = Array.from({ length: 80 }, (_, i) => [i, 0.5 + Math.sin(i/8)*0.05]);
  const [bpm, setBpm] = React.useState(131);
  React.useEffect(() => {
    const id = setInterval(() => setBpm(128 + Math.round(Math.sin(Date.now()/3000)*5)), 1500);
    return () => clearInterval(id);
  }, []);
  return (
    <>
      {/* === ANATOMICAL CYBER HEART — top of dashboard === */}
      <div className="card" style={{ padding: 0, overflow: "hidden", marginBottom: 12, position: "relative" }}>
        <div style={{
          position: "absolute", top: 12, left: 14, zIndex: 2,
          fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--amber)", letterSpacing: "0.16em"
        }}>
          // CARDIAC TELEMETRY
        </div>
        <div style={{
          position: "absolute", top: 12, right: 14, zIndex: 2,
          fontFamily: "JetBrains Mono", fontSize: 10, color: "#e26565"
        }}>
          ● LIVE · TACHY
        </div>
        {window.CyberHeart && <window.CyberHeart bpm={bpm} size={380}/>}
        {/* Scroll hint */}
        <div style={{
          position: "absolute", bottom: 10, left: "50%", transform: "translateX(-50%)",
          fontFamily: "JetBrains Mono", fontSize: 9, color: "var(--fg-3)", letterSpacing: "0.2em",
          opacity: 0.6, animation: "scrollHint 2s ease-in-out infinite"
        }}>
          ▼ SCROLL FOR ANALYTICS ▼
        </div>
      </div>
      <style>{`@keyframes scrollHint { 0%, 100% { opacity: 0.3; transform: translateX(-50%) translateY(0); } 50% { opacity: 0.7; transform: translateX(-50%) translateY(3px); } }`}</style>

      <div className="dash-grid">
        <div className="card">
          <div className="card-head">
            <div className="card-title">АКТИВНІСТЬ ПО ГОДИНАХ <span className="n">// КОЛИ ВІН ТУТ</span></div>
            <div className="card-meta" style={{color:"#7fd49a"}}>162 повідомлень за сьогодні</div>
          </div>
          <BarChart data={hours} highlightIdx={16} axis={["00","04","08","12","16","20"].map((x,i)=>x)}/>
          <div className="bars-axis" style={{justifyContent:"space-between",display:"flex",marginTop:6}}>
            <span>00:00</span><span>06:00</span><span>12:00</span><span>18:00</span><span>23:59</span>
          </div>
        </div>
        <div className="card">
          <div className="card-head">
            <div className="card-title">ЕМОЦІЇ <span className="n">// 7 ДНІВ</span></div>
          </div>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "center", padding: "8px 0" }}>
            <Donut
              segments={[
                { value: 58, color: "#ffb020" },
                { value: 18, color: "#8aa9ff" },
                { value: 12, color: "#7fd49a" },
                { value: 12, color: "#3a382f" },
              ]}
              label="58%" sublabel="Сум"
            />
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 6, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-2)", justifyContent: "center" }}>
            <span><span style={{display:"inline-block",width:8,height:8,background:"#ffb020",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>СУМ 58%</span>
            <span><span style={{display:"inline-block",width:8,height:8,background:"#8aa9ff",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>ТРИВ 18%</span>
            <span><span style={{display:"inline-block",width:8,height:8,background:"#7fd49a",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>СПОК 12%</span>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 10 }}>
        <div className="card-head">
          <div className="card-title">КРИВА ТРАЄКТОРІЇ <span className="n">// НЕЙТИНГ ДО БЛИЗЬКОСТІ</span></div>
          <div style={{ display: "flex", gap: 12, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-2)" }}>
            <span><span style={{display:"inline-block",width:14,height:2,background:"#ffb020",marginRight:4,verticalAlign:"middle"}}/>ACTUAL</span>
            <span><span style={{display:"inline-block",width:14,height:2,borderTop:"2px dashed #8aa9ff",marginRight:4,verticalAlign:"middle"}}/>TARGET</span>
          </div>
        </div>
        <LineChart series={[
          { points: targetPts, color: "#8aa9ff", dash: "4 4" },
          { points: moodPts, color: "#ffb020", glow: true, fill: "#ffb020" },
        ]} h={180}/>
      </div>

      <div className="dash-row-4" style={{ marginTop: 10 }}>
        <div className="kpi amber">
          <div className="l">CONFIDENCE</div>
          <div className="v">46%</div>
          <div className="spark"><Spark values={[20,30,28,40,55,48,46]} color="#ffb020"/></div>
        </div>
        <div className="kpi">
          <div className="l">CONFLICTS</div>
          <div className="v" style={{color:"#7fd49a"}}>0</div>
          <div className="spark"><Spark values={[2,1,2,0,1,0,0]} color="#7fd49a"/></div>
        </div>
        <div className="kpi">
          <div className="l">INSIGHTS</div>
          <div className="v" style={{color:"#8aa9ff"}}>13</div>
          <div className="spark"><Spark values={[3,5,8,9,11,12,13]} color="#8aa9ff"/></div>
        </div>
        <div className="kpi">
          <div className="l">RUPTURES</div>
          <div className="v" style={{color:"#e26565"}}>0</div>
          <div className="spark"><Spark values={[1,0,0,1,0,0,0]} color="#e26565"/></div>
        </div>
      </div>

      <div className="dash-row-2" style={{ marginTop: 10 }}>
        <div className="card">
          <div className="card-head">
            <div className="card-title">MOOD 24H <span className="n">// ДРАЙВ НАСТРОЮ</span></div>
            <div className="card-meta" style={{color:"#ffb020"}}>+ зараз</div>
          </div>
          <LineChart series={[{
            points: Array.from({length: 50}, (_, i) => [i, 0.3 + Math.sin(i/4)*0.2 + (i>35? 0.2:0)]),
            color: "#ffb020", glow: true, fill: "#ffb020",
          }]} h={120}/>
        </div>
        <div className="card">
          <div className="card-head">
            <div className="card-title">WEEKLY HEATMAP <span className="n">// 7 × 24</span></div>
            <div className="card-meta">8 квіт — 7 трав</div>
          </div>
          <Heatmap rows={7} cols={24} data={Array.from({length:168},() => Math.random()*0.9)}/>
        </div>
      </div>

      <div className="dash-row-2" style={{ marginTop: 10 }}>
        {window.AvatarPanel && <window.AvatarPanel emotion="concerned" bpm={131}/>}
        <div className="card">
          <div className="card-head">
            <div className="card-title">SESSION CONTEXT</div>
            <div className="card-meta">live · session 04/26</div>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr auto", rowGap: 8, fontFamily: "JetBrains Mono", fontSize: 11 }}>
            <span style={{color:"var(--fg-3)"}}>active model</span><span style={{color:"var(--amber-hi)"}}>kokonoe-mini · v0.7.4</span>
            <span style={{color:"var(--fg-3)"}}>context window</span><span>12.4k / 32k tok</span>
            <span style={{color:"var(--fg-3)"}}>messages today</span><span style={{color:"#7fd49a"}}>162</span>
            <span style={{color:"var(--fg-3)"}}>memory writes</span><span style={{color:"var(--amber)"}}>13</span>
            <span style={{color:"var(--fg-3)"}}>last touch</span><span>20:32:18</span>
            <span style={{color:"var(--fg-3)"}}>tone bias</span><span style={{color:"#8aa9ff"}}>low-key · observant</span>
          </div>
          <div style={{ marginTop: 14, padding: "10px 12px", background: "rgba(255,176,32,0.06)", border: "1px solid var(--line-3)", borderRadius: 6, fontSize: 12, color: "var(--fg-1)", lineHeight: 1.5 }}>
            <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--amber)", letterSpacing: "0.16em", marginBottom: 4 }}>// CURRENT INTENT</div>
            Тримати тишу. Не пропонувати рішень. Бути присутньою без тиску.
          </div>
        </div>
      </div>
    </>
  );
}

function DevTab() {
  const days = [
    { day: "Mon", commits: 18, lines: 220 },
    { day: "Tue", commits: 28, lines: 410 },
    { day: "Wed", commits: 42, lines: 680 },
    { day: "Thu", commits: 56, lines: 920 },
    { day: "Fri", commits: 71, lines: 1230 },
    { day: "Sat", commits: 38, lines: 540 },
    { day: "Sun", commits: 22, lines: 280 },
  ];
  const burnPts = Array.from({length: 28}, (_, i) => [i, Math.max(0, 100 - i*3.4 - Math.sin(i/3)*4)]);
  const idealPts = Array.from({length: 28}, (_, i) => [i, 100 - i*3.6]);
  return (
    <>
      <div className="dash-grid">
        <div className="card">
          <div className="card-head">
            <div className="card-title">GIT ACTIVITY & VELOCITY <span className="n">// ОСТАННІЙ ТИЖДЕНЬ</span></div>
            <div className="card-meta" style={{color:"#ffb020"}}>34 дні Pyrite сьогодні</div>
          </div>
          <div style={{ display: "flex", alignItems: "flex-end", gap: 12, height: 200 }}>
            {days.map((d, i) => (
              <div key={i} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", gap: 4 }}>
                <div style={{ display: "flex", alignItems: "flex-end", gap: 3, height: "100%", width: "100%", justifyContent: "center" }}>
                  <div style={{
                    width: "40%",
                    height: `${(d.commits/80)*100}%`,
                    background: "linear-gradient(180deg, #b48cff, #6a3fc0)",
                    borderRadius: "2px 2px 0 0"
                  }}/>
                  <div style={{
                    width: "40%",
                    height: `${(d.lines/1300)*100}%`,
                    background: "linear-gradient(180deg, #ffc94a, #d18a00)",
                    borderRadius: "2px 2px 0 0",
                    boxShadow: "0 0 8px rgba(255,176,32,0.4)"
                  }}/>
                </div>
                <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>{d.day}</div>
              </div>
            ))}
          </div>
          <div style={{ display: "flex", gap: 14, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-2)", marginTop: 10 }}>
            <span><span style={{display:"inline-block",width:8,height:8,background:"#b48cff",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>commits</span>
            <span><span style={{display:"inline-block",width:8,height:8,background:"#ffc94a",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>lines (kloc)</span>
          </div>
        </div>

        <div className="card">
          <div className="card-head"><div className="card-title">РОЗПОДІЛ ЧАСУ <span className="n">// 7 ДНІВ</span></div></div>
          <div style={{ display: "flex", justifyContent: "center", padding: "8px 0" }}>
            <Donut
              segments={[
                { value: 39, color: "#ffb020" },
                { value: 22, color: "#b48cff" },
                { value: 18, color: "#7fd49a" },
                { value: 13, color: "#8aa9ff" },
                { value: 8,  color: "#3a382f" },
              ]}
              label="39%" sublabel="CODE"
            />
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 4, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-2)", marginTop: 4 }}>
            <span><i style={{display:"inline-block",width:8,height:8,background:"#ffb020",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>Kokonoe 39%</span>
            <span><i style={{display:"inline-block",width:8,height:8,background:"#b48cff",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>Pyrite 22%</span>
            <span><i style={{display:"inline-block",width:8,height:8,background:"#7fd49a",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>Cure 18%</span>
            <span><i style={{display:"inline-block",width:8,height:8,background:"#8aa9ff",borderRadius:2,marginRight:4,verticalAlign:"middle"}}/>Identity 13%</span>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 10 }}>
        <div className="card-head">
          <div className="card-title">BURNDOWN CHART ПОТОЧНОГО СПРИНТУ</div>
          <div style={{ display: "flex", gap: 12, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-2)" }}>
            <span><span style={{display:"inline-block",width:14,height:2,background:"#ffb020",marginRight:4,verticalAlign:"middle"}}/>actual</span>
            <span><span style={{display:"inline-block",width:14,height:2,borderTop:"2px dashed #8aa9ff",marginRight:4,verticalAlign:"middle"}}/>ideal</span>
          </div>
        </div>
        <LineChart series={[
          { points: idealPts, color: "#8aa9ff", dash: "4 4" },
          { points: burnPts, color: "#ffb020", glow: true, fill: "#ffb020" },
        ]} h={220}/>
        <div style={{fontFamily:"JetBrains Mono",fontSize:10,color:"var(--fg-3)",marginTop:6}}>+ actual</div>
      </div>

      <div className="dash-row-4" style={{ marginTop: 10 }}>
        <div className="kpi"><div className="l">ПОВІДОМЛЕНЬ/ГОДИНУ</div><div className="v">0_</div></div>
        <div className="kpi"><div className="l">ДНІ ЗВ'ЯЗАНОСТІ</div><div className="v amber" style={{color:"#ffc94a"}}>06</div><div style={{fontFamily:"JetBrains Mono",fontSize:10,color:"var(--fg-3)"}}>06 / 0</div></div>
        <div className="kpi"><div className="l">ПІДПИСАНИХ ФАЙЛІВ</div><div className="v" style={{color:"#7fd49a"}}>15</div><div style={{fontFamily:"JetBrains Mono",fontSize:10,color:"var(--fg-3)"}}>15/15</div></div>
        <div className="kpi"><div className="l">ЗВ'ЯЗАНІ ЗАПРОСИ</div><div className="v">0</div><div style={{fontFamily:"JetBrains Mono",fontSize:10,color:"var(--fg-3)"}}>0 запитів</div></div>
      </div>
    </>
  );
}

const memFacts = [
  { txt: "я люблю тебе", tag: "preference", imp: 0.95, seen: 7 },
  { txt: "Він відчуває радість.", tag: "observation", imp: 0.85, seen: 2 },
  { txt: "Він почувається самотньо.", tag: "observation", imp: 0.83, seen: 3 },
  { txt: "Він не хоче йти на курси.", tag: "observation", imp: 0.78, seen: 1 },
  { txt: "Він хоче інтимного контакту.", tag: "desire", imp: 0.92, seen: 5 },
  { txt: "Він пишає собі за програму.", tag: "observation", imp: 0.81, seen: 2 },
  { txt: "Він хоче їсти.", tag: "observation", imp: 0.6, seen: 1 },
  { txt: "Він відчуває самотність.", tag: "observation", imp: 0.84, seen: 4 },
  { txt: 'Actually, looking at "ти будеш такою як я захочу" → This implies he has a "wanting" or a "desire". But it doesn\'t say what he likes in general. It\'s too specific to this interaction.', tag: "reflection", imp: 0.88, seen: 1 },
  { txt: "Він не має настрою щось робити або говорити.", tag: "observation", imp: 0.74, seen: 1 },
  { txt: "Він хоче займатися YouTube-контентом за допомогою ШІ.", tag: "observation", imp: 0.86, seen: 1 },
  { txt: "У нього сьогодні день народження.", tag: "observation", imp: 0.93, seen: 1 },
  { txt: "я хочу щоб ти була собою", tag: "desire", imp: 0.95, seen: 1 },
];
function MemoryTab() {
  return (
    <>
      <div className="card" style={{ marginBottom: 10 }}>
        <div style={{ display: "flex", alignItems: "baseline", gap: 18 }}>
          <div className="card-title">ПАМ'ЯТЬ — ФАКТИ</div>
          <div style={{ fontFamily: "Space Grotesk", fontSize: 28, fontWeight: 600, color: "#7fd49a" }}>
            13 <span style={{ fontSize: 12, color: "var(--fg-3)", fontFamily: "JetBrains Mono" }}>записів</span>
          </div>
          <div style={{ fontFamily: "Space Grotesk", fontSize: 28, fontWeight: 600, color: "#ffb020" }}>
            2 <span style={{ fontSize: 12, color: "var(--fg-3)", fontFamily: "JetBrains Mono" }}>підтверджено</span>
          </div>
        </div>
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {memFacts.map((f, i) => (
          <div key={i} className="card" style={{ padding: "10px 14px" }}>
            <div style={{ color: "var(--fg-0)", fontSize: 13, lineHeight: 1.5, marginBottom: 4 }}>{f.txt}</div>
            <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)", display: "flex", gap: 14 }}>
              <span style={{color: f.tag === "desire" ? "#ffc94a" : f.tag === "reflection" ? "#b48cff" : "#7fd49a"}}>{f.tag}</span>
              <span>importance <b style={{color:"var(--fg-1)"}}>{f.imp.toFixed(2)}</b></span>
              <span>seen <b style={{color:"var(--fg-1)"}}>{f.seen}</b></span>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}

function SystemTab() {
  return (
    <>
      <div className="card-title" style={{ marginBottom: 10 }}>СИСТЕМА</div>
      <div className="dash-row-2" style={{ gap: 10 }}>
        <div className="card">
          <div className="card-title">MAIN APP</div>
          <div style={{ marginTop: 10, fontFamily: "JetBrains Mono", color: "#7fd49a", fontSize: 18 }}>
            online :8765
          </div>
          <div style={{ fontFamily: "JetBrains Mono", fontSize: 11, color: "var(--fg-3)", marginTop: 6, wordBreak: "break-all" }}>
            https://kokonoe-discoding.qwa-animated.tryclouflare.com
          </div>
        </div>
        <div className="card">
          <div className="card-title">TUNNEL</div>
          <div style={{ marginTop: 10, fontFamily: "JetBrains Mono", color: "#7fd49a", fontSize: 18 }}>running</div>
        </div>
        <div className="card">
          <div className="card-title">UPTIME</div>
          <div style={{ marginTop: 10, fontFamily: "Space Grotesk", color: "var(--fg-0)", fontSize: 22, fontWeight: 500 }}>0m 31s</div>
        </div>
        <div className="card">
          <div className="card-title">MEMORY (RAM)</div>
          <div style={{ marginTop: 10, fontFamily: "Space Grotesk", color: "var(--amber-hi)", fontSize: 22, fontWeight: 500 }}>350 MB</div>
          <div style={{ marginTop: 6, height: 4, background: "rgba(255,255,255,0.05)", borderRadius: 2, overflow: "hidden" }}>
            <div style={{ width: "22%", height: "100%", background: "linear-gradient(90deg, var(--amber), var(--amber-hi))", boxShadow: "0 0 6px var(--amber-glow)" }}/>
          </div>
        </div>
      </div>

      <div className="dash-row-2" style={{ gap: 10, marginTop: 10 }}>
        <div className="card">
          <div className="card-title">LOG STREAM <span className="n">// LIVE</span></div>
          <div style={{ marginTop: 10, fontFamily: "JetBrains Mono", fontSize: 10, lineHeight: 1.7 }}>
            <div><span style={{color:"var(--fg-3)"}}>20:32:01</span> <span style={{color:"#7fd49a"}}>INFO</span> mtproto.connect ok</div>
            <div><span style={{color:"var(--fg-3)"}}>20:32:04</span> <span style={{color:"#7fd49a"}}>INFO</span> vault.scan 2,341 nodes</div>
            <div><span style={{color:"var(--fg-3)"}}>20:32:08</span> <span style={{color:"#ffb020"}}>WARN</span> heart.bpm above_baseline</div>
            <div><span style={{color:"var(--fg-3)"}}>20:32:11</span> <span style={{color:"#7fd49a"}}>INFO</span> memory.write fact #134</div>
            <div><span style={{color:"var(--fg-3)"}}>20:32:18</span> <span style={{color:"#8aa9ff"}}>DEBG</span> mono.tick t+221s</div>
            <div><span style={{color:"var(--fg-3)"}}>20:32:24</span> <span style={{color:"#7fd49a"}}>INFO</span> obsidian.sync ok</div>
          </div>
        </div>
        <div className="card">
          <div className="card-title">PROCESSES</div>
          <div style={{ marginTop: 10, fontFamily: "JetBrains Mono", fontSize: 11, display: "grid", gridTemplateColumns: "1fr auto auto", gap: "6px 14px" }}>
            <span>kokonoe.core</span><span style={{color:"#7fd49a"}}>●</span><span>180 MB</span>
            <span>monologue.engine</span><span style={{color:"#7fd49a"}}>●</span><span>62 MB</span>
            <span>mtproto.client</span><span style={{color:"#7fd49a"}}>●</span><span>48 MB</span>
            <span>obsidian.bridge</span><span style={{color:"#7fd49a"}}>●</span><span>34 MB</span>
            <span>cardiac.poll</span><span style={{color:"#ffb020"}}>●</span><span>16 MB</span>
            <span>vault.indexer</span><span style={{color:"var(--fg-3)"}}>○</span><span>idle</span>
          </div>
        </div>
      </div>
    </>
  );
}

function Dashboard({ tab, setTab, accent }) {
  return (
    <>
      <div className="tabs">
        {["Neural","Dev","Memory","System"].map(t => (
          <div key={t} className={"tab" + (tab === t ? " active" : "")} onClick={() => setTab(t)}>{t}</div>
        ))}
      </div>
      <div className="main-body">
        {tab === "Neural" && <NeuralTab/>}
        {tab === "Dev" && <DevTab/>}
        {tab === "Memory" && <MemoryTab/>}
        {tab === "System" && <SystemTab/>}
      </div>
    </>
  );
}

window.Dashboard = Dashboard;
