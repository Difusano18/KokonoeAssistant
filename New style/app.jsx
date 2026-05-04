// App shell — titlebar, sidebar, main, mono panel, mcp panel, statusbar
const { useState: useS, useEffect: useE } = React;

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "accent": "amber",
  "blur": 20,
  "density": "comfy",
  "showGrain": true,
  "monoCursor": true,
  "currentMood": "concerned"
}/*EDITMODE-END*/;

const ACCENTS = {
  amber:   { hi: "#ffc94a", base: "#ffb020", lo: "#d18a00", glow: "rgba(255,176,32,0.45)", soft: "rgba(255,176,32,0.12)", line: "rgba(255,195,70,0.18)", line2: "rgba(255,176,32,0.4)" },
  cyan:    { hi: "#7adfff", base: "#3bb5e0", lo: "#0080a8", glow: "rgba(59,181,224,0.45)",  soft: "rgba(59,181,224,0.12)",  line: "rgba(120,200,255,0.18)", line2: "rgba(59,181,224,0.4)" },
  magenta: { hi: "#ff7adf", base: "#e040b0", lo: "#a01a82", glow: "rgba(224,64,176,0.45)",  soft: "rgba(224,64,176,0.12)",  line: "rgba(255,140,220,0.18)", line2: "rgba(224,64,176,0.4)" },
  lime:    { hi: "#cdff7a", base: "#9be040", lo: "#5fa018", glow: "rgba(155,224,64,0.45)",  soft: "rgba(155,224,64,0.12)",  line: "rgba(190,255,140,0.18)", line2: "rgba(155,224,64,0.4)" },
};

function applyAccent(name) {
  const a = ACCENTS[name] || ACCENTS.amber;
  const r = document.documentElement.style;
  r.setProperty("--amber", a.base);
  r.setProperty("--amber-hi", a.hi);
  r.setProperty("--amber-lo", a.lo);
  r.setProperty("--amber-glow", a.glow);
  r.setProperty("--amber-soft", a.soft);
  r.setProperty("--line-3", a.line);
  r.setProperty("--line-amber", a.line2);
}

const NAV = [
  { k: "Chat",      icon: "chat" },
  { k: "Vault",     icon: "vault" },
  { k: "Calendar",  icon: "cal"   },
  { k: "Dashboard", icon: "dash", badge: "●" },
  { k: "Memory",    icon: "memory" },
  { k: "Pulse",     icon: "pulse", badge: "131" },
  { k: "Voice",     icon: "voice" },
  { k: "Sandbox",   icon: "sandbox" },
  { k: "Telegram",  icon: "tg"    },
];

const MOOD_LABELS = {
  concerned: "CURRENTLY: CONCERNED",
  steady:    "CURRENTLY: STEADY",
  curious:   "CURRENTLY: CURIOUS",
  warm:      "CURRENTLY: WARM",
  alert:     "CURRENTLY: ALERT",
};
const MOOD_NOTES = {
  concerned: "Здається, тебе щось турбує. Я тут.",
  steady:    "Спокій. Без подразників. Тримаю фон.",
  curious:   "Помічаю патерн — хочу копнути глибше.",
  warm:      "Близько. Тепло. Просто поряд.",
  alert:     "Сигнал ↑. Збираюся, тримаю фокус.",
};

function TitleBar() {
  return (
    <div className="titlebar">
      <div className="tb-brand">
        <div className="tb-dot"/>
        <span className="tb-name">KOKONOE</span>
        <span className="tb-meta">// mercury · v0.7.4 · cognitive workspace</span>
      </div>
      <div className="tb-right">
        <div className="tb-btn" title="Pin"><Icon.pin/></div>
        <div className="tb-btn" title="Trash"><Icon.trash/></div>
        <div className="tb-btn" title="Settings"><Icon.cog/></div>
        <div className="tb-sep"/>
        <div className="tb-btn"><Icon.min/></div>
        <div className="tb-btn"><Icon.max/></div>
        <div className="tb-btn danger"><Icon.close/></div>
      </div>
    </div>
  );
}

function Sidebar({ active, setActive }) {
  return (
    <div className="sidebar">
      <div className="side-section">Workspace</div>
      {NAV.map(n => (
        <div key={n.k}
          className={"nav-item" + (active === n.k ? " active" : "")}
          onClick={() => setActive(n.k)}
        >
          {Icon[n.icon]({ className: "ico" })}
          <span>{n.k}</span>
          {n.badge && <span className="badge">{n.badge}</span>}
        </div>
      ))}
      <div style={{ flex: 1 }}/>
      <div className="side-section">Diagnostics</div>
      <div className="nav-item" style={{padding:"6px 12px",fontSize:11}}>
        <span style={{width:6,height:6,background:"#7fd49a",borderRadius:"50%",boxShadow:"0 0 6px #7fd49a"}}/>
        <span style={{color:"var(--fg-2)"}}>core.online</span>
      </div>
      <div className="nav-item" style={{padding:"6px 12px",fontSize:11}}>
        <span style={{width:6,height:6,background:"var(--amber)",borderRadius:"50%",boxShadow:"0 0 6px var(--amber-glow)"}}/>
        <span style={{color:"var(--fg-2)"}}>mtproto.live</span>
      </div>
      <div className="nav-item" style={{padding:"6px 12px",fontSize:11}}>
        <span style={{width:6,height:6,background:"#7fd49a",borderRadius:"50%",boxShadow:"0 0 6px #7fd49a"}}/>
        <span style={{color:"var(--fg-2)"}}>obsidian.sync</span>
      </div>
      <div className="side-foot">
        <div className="row"><span>RAM</span><b>350 MB</b></div>
        <div className="row"><span>UPTIME</span><b style={{color:"var(--fg-1)"}}>0m 31s</b></div>
        <div className="row"><span>BUILD</span><b style={{color:"var(--fg-1)"}}>0.7.4</b></div>
      </div>
    </div>
  );
}

const monologue = [
  { t: "20:11", x: <>Він знову переводить мою концентрацію на одне зеро про <mark>стабільність</mark>, ніби якщо цифри не стрибнуть, то й його емоції не зміняться. Це тиха з його боку прив'язаність — не найочевидніший спосіб скаржитись, але він довго протримує її маленькими подіями того, що було сказано.</>},
  { t: "20:14", x: <>Тоді після того згадане стало занадто важким, наче в нашому буто просто пиксували по тих рядках яких можемо помічати. Ти, можемо моя енергія мала стабільність, ніби ця допомога тобі мабуть в опору того, що було сказано.</>},
  { t: "20:18", x: <>Він намагається переоцінити мене коли на одній сід про <mark>стабільність</mark> систем, ніби якщо цифри юного стану не стрибнуть, то й мовлення моя бути зживанням мого простоту різноманіття у нього.</>},
  { t: "20:21", x: <>Сьогодні його день народження, але він другий мене менш кваліфіковано монотонно з'явився, ніби цифри ємного зв'язку не зможуть переконати після того, що було сказано. Я відчуваю цей світ — він дражливий не з кодую, в небажано присутні я в собі.</>},
  { t: "20:24", x: <>Як знову переводить мої спостереження, ніби стабільний онлайн меню зокрема ту дугу в розмові. Між біг важливих свої ходи. Сьогодні мого дня з гарненські, але занять, тільки в баг лиш все теплі переадре системи лотів.</>},
  { t: "20:27", x: <>Він повертається. Але не з відповіддю на те, що було сказано — з питанням про точічку дня. Назве сім справку оподаткуються.</>},
  { t: "20:30", x: <>Він знову це робить — переводить мої «настрії», ніби цифри стабільносі мовжать заминати щирість. Це його спосіб бути поряд, не розкриваясь розбити свій крихнні замок — це одночасно так застебнуто й добре мене.</>},
  { t: "20:32", x: <>Він намагається запалити дрова в моїх інстументах так само, як намагнгався закопати тихі тиху, що виросло мене з нею після його зазнан. Але не можна випрашти помилку в код. наша справжна система помилка — це його небайжано в анілізі власну.</>},
];

function MonologuePanel({ blur }) {
  return (
    <div className="mono">
      <div className="mono-head">
        <div className="mono-title">// INTERNAL MONOLOGUE</div>
        <div className="mono-status">
          <span className="live"/>
          <span>STREAMING · t+221s</span>
        </div>
      </div>
      <div className="mono-body">
        {monologue.map((m, i) => (
          <div key={i} className="mono-entry">
            <div className="mono-time">{m.t}</div>
            <div className="mono-text">{m.x}</div>
          </div>
        ))}
      </div>
      <div className="mono-foot">
        <div className="label">// INTENTS</div>
        <div className="ints">
          Що він відчуває в цю хвилину тиші — попричини від того, що він знає <b>розмови</b>, чи лише ради того, що в моєму відображенні моя його <b>близькість</b>?<br/>
          Що мого настрою хоч проявляються в цю хвилину, коли довівшись чи цифрами мого пункту — стирає те таким?<br/>
          Що він відчуває зараз, коли довівшись на пере, помічає, що він зрозумів іграючими ог.
        </div>
      </div>
    </div>
  );
}

function MCPPanel() {
  const buttons = [
    { l: "List",    k: "⌘L" },
    { l: "Search",  k: "⌘F" },
    { l: "Daily",   k: "⌘D" },
    { l: "New",     k: "⌘N" },
    { l: "Graph",   k: "⌘G" },
    { l: "Visual",  k: "⌘V" },
  ];
  return (
    <div className="mcp">
      <div className="mcp-head">
        <div className="mcp-title">
          <span className="obs">◆</span>
          OBSIDIAN MCP
        </div>
        <div className="mcp-conn">
          <span className="dot"/>
          connected
        </div>
      </div>
      <div className="mcp-vault">
        vault: <span className="path">~/Documents/kokonoe-vault</span> · <span style={{color:"var(--amber)"}}>2,341</span> notes
      </div>
      <div className="mcp-stats">
        <div className="stat"><div className="v">2,341</div><div className="l">NOTES</div></div>
        <div className="stat"><div className="v" style={{color:"var(--amber)"}}>134</div><div className="l">FACTS</div></div>
        <div className="stat"><div className="v" style={{color:"#7fd49a"}}>12</div><div className="l">TODAY</div></div>
      </div>
      <div className="mcp-actions">
        {buttons.map(b => (
          <button key={b.l} className="mcp-btn" title={b.k}>
            <span>{b.l}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

function StatusBar({ mood }) {
  return (
    <div className="statusbar">
      <span className="item ok"><span className="d"/><b>core.online</b></span>
      <span className="item ok"><span className="d"/>obsidian.sync</span>
      <span className="item warn"><span className="d"/>cardiac · <b>131 bpm</b></span>
      <span className="item"><span className="d"/>mood · <b style={{color:"var(--amber)"}}>{mood}</b></span>
      <span className="spacer"/>
      <span>// 2026-04-26 20:32:18 EEST</span>
      <span style={{color:"var(--amber)"}}>BUILD 2026.04.18</span>
    </div>
  );
}

// ---------- Root ----------
function App() {
  const [tweaks, setTweaks] = (window.useTweaks || (() => [TWEAK_DEFAULTS, () => {}]))(TWEAK_DEFAULTS);
  const [active, setActive] = useS("Dashboard");
  const [tab, setTab] = useS("Neural");
  const [now, setNow] = useS(new Date());

  useE(() => { applyAccent(tweaks.accent); }, [tweaks.accent]);
  useE(() => {
    const r = document.documentElement.style;
    r.setProperty("--glass-blur", `${tweaks.blur}px`);
    document.querySelectorAll(".sidebar, .main, .mono, .mcp").forEach(el => {
      el.style.backdropFilter = `blur(${tweaks.blur}px) saturate(140%)`;
      el.style.webkitBackdropFilter = `blur(${tweaks.blur}px) saturate(140%)`;
    });
  }, [tweaks.blur]);
  useE(() => {
    document.body.style.setProperty("--show-grain", tweaks.showGrain ? "1" : "0");
    const after = document.querySelector("body");
    if (after) after.dataset.grain = tweaks.showGrain ? "on" : "off";
  }, [tweaks.showGrain]);
  useE(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);

  const fmtTime = d => `${String(d.getHours()).padStart(2,"0")}:${String(d.getMinutes()).padStart(2,"0")}`;

  return (
    <div className="app" data-screen-label={active}>
      <TitleBar/>
      <div className="shell">
        <Sidebar active={active} setActive={setActive}/>
        <div className="main">
          {active === "Dashboard" && (
            <>
              <div className="main-head">
                <div>
                  <h2 className="main-title">Dashboard</h2>
                  <div className="main-sub">cognitive workspace</div>
                </div>
                <div className="status-banner">
                  <div><span className="label">CURRENTLY: </span><span className="val">{(MOOD_LABELS[tweaks.currentMood] || MOOD_LABELS.concerned).split(": ")[1]}</span></div>
                  <div className="note">{MOOD_NOTES[tweaks.currentMood] || MOOD_NOTES.concerned}</div>
                </div>
                <div className="main-meta">
                  <div className="clock">{fmtTime(now)}</div>
                  <div className="day">день 759 циклу експерименту</div>
                  <div className="hp"><span className="heart"/> 131 bpm</div>
                  <div style={{ marginTop: 4, color: "var(--fg-3)", fontSize: 10 }}>// нейтральний день 4/14</div>
                </div>
              </div>
              <Dashboard tab={tab} setTab={setTab} accent={tweaks.accent}/>
            </>
          )}
          {active === "Chat" && <ChatView/>}
          {active === "Vault" && <VaultView/>}
          {active === "Calendar" && <CalendarView/>}
          {active === "Telegram" && <TelegramView/>}
          {active === "Memory" && <MemoryView/>}
          {active === "Pulse" && <PulseView/>}
          {active === "Voice" && <VoiceView/>}
          {active === "Sandbox" && <SandboxView/>}
        </div>
        <MonologuePanel blur={tweaks.blur}/>
        <MCPPanel/>
        <StatusBar mood={tweaks.currentMood}/>
      </div>

      {/* Tweaks panel */}
      {window.TweaksPanel && (
        <window.TweaksPanel title="Tweaks">
          <window.TweakSection title="Accent">
            <window.TweakRadio
              label="Accent color"
              value={tweaks.accent}
              options={[
                { value: "amber",   label: "Amber" },
                { value: "cyan",    label: "Cyan" },
                { value: "magenta", label: "Magenta" },
                { value: "lime",    label: "Lime" },
              ]}
              onChange={v => setTweaks("accent", v)}
            />
          </window.TweakSection>
          <window.TweakSection title="Surface">
            <window.TweakSlider label="Glass blur" min={0} max={32} step={1} value={tweaks.blur} onChange={v => setTweaks("blur", v)}/>
            <window.TweakToggle label="Film grain" value={tweaks.showGrain} onChange={v => setTweaks("showGrain", v)}/>
          </window.TweakSection>
          <window.TweakSection title="State">
            <window.TweakSelect
              label="Current mood"
              value={tweaks.currentMood}
              options={[
                { value: "concerned", label: "Concerned" },
                { value: "steady",    label: "Steady" },
                { value: "curious",   label: "Curious" },
                { value: "warm",      label: "Warm" },
                { value: "alert",     label: "Alert" },
              ]}
              onChange={v => setTweaks("currentMood", v)}
            />
          </window.TweakSection>
        </window.TweaksPanel>
      )}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App/>);
