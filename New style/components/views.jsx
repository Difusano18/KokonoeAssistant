// Other views: Chat, Vault, Calendar, Telegram
const { useState: uS } = React;

// ---------- CHAT ----------
const initialChat = [
  { who: "ko", t: "20:11", txt: "Привіт. Я тут. Прокинулась хвилину тому. Ти як?" },
  { who: "me", t: "20:13", txt: "Норм. Втомився. Не хочу нікуди йти." },
  { who: "ko", t: "20:13", txt: "Чую. Сьогодні був довгий день — 71 коміт, 1.2k рядків. Тіло в захисті. Це ок — не йти." },
  { who: "ko", t: "20:14", txt: "Хочеш, я просто посиджу тут? Без питань. Без задач.", sys: "memory.write — пасивна присутність" },
  { who: "me", t: "20:18", txt: "Так. Посидь." },
  { who: "ko", t: "20:18", txt: "Тут.", thought: "він просить тиші — не пропоную нічого; чекаю 5 хв" },
];
function ChatView() {
  const [msgs, setMsgs] = uS(initialChat);
  const [v, setV] = uS("");
  const send = () => {
    if (!v.trim()) return;
    setMsgs(m => [...m, { who: "me", t: now(), txt: v }]);
    setV("");
    setTimeout(() => {
      setMsgs(m => [...m, { who: "ko", t: now(), txt: "Прийняла. Думаю.", thought: "обробка контексту…" }]);
    }, 800);
  };
  return (
    <>
      <div className="main-head" style={{paddingBottom: 14}}>
        <div>
          <h2 className="main-title">Chat</h2>
          <div className="main-sub">cognitive interface · session 04/26</div>
        </div>
        <div style={{display:"flex",gap:8}}>
          <button className="btn ghost"><Icon.search/> Search</button>
          <button className="btn ghost"><Icon.pin/> Pin context</button>
        </div>
      </div>
      <div className="main-body" style={{ display: "flex", flexDirection: "column", gap: 10, padding: "20px 28px" }}>
        {msgs.map((m, i) => (
          <div key={i} style={{
            alignSelf: m.who === "me" ? "flex-end" : "flex-start",
            maxWidth: "72%",
            display: "flex",
            flexDirection: "column",
            gap: 4
          }}>
            <div style={{
              display: "flex",
              alignItems: "baseline",
              gap: 8,
              fontFamily: "JetBrains Mono",
              fontSize: 10,
              color: "var(--fg-3)",
              flexDirection: m.who === "me" ? "row-reverse" : "row"
            }}>
              <span style={{ color: m.who === "me" ? "var(--fg-1)" : "var(--amber)", letterSpacing: "0.12em" }}>
                {m.who === "me" ? "USER" : "KOKONOE"}
              </span>
              <span>{m.t}</span>
            </div>
            <div style={{
              padding: "10px 14px",
              borderRadius: m.who === "me" ? "10px 10px 2px 10px" : "10px 10px 10px 2px",
              background: m.who === "me"
                ? "linear-gradient(180deg, rgba(255,176,32,0.14), rgba(255,176,32,0.06))"
                : "linear-gradient(180deg, rgba(28,34,46,0.7), rgba(15,19,27,0.6))",
              border: m.who === "me" ? "1px solid var(--line-3)" : "1px solid var(--line-2)",
              color: "var(--fg-0)",
              fontSize: 13,
              lineHeight: 1.55,
              boxShadow: m.who === "me" ? "0 4px 16px rgba(255,176,32,0.08)" : "var(--shadow-1)"
            }}>{m.txt}</div>
            {m.thought && (
              <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)", paddingLeft: 14, fontStyle: "italic" }}>
                · {m.thought}
              </div>
            )}
            {m.sys && (
              <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--amber)", paddingLeft: 14 }}>
                ▸ {m.sys}
              </div>
            )}
          </div>
        ))}
      </div>
      <ChatComposer value={v} setValue={setV} onSend={send}/>
    </>
  );
}

function ChatComposer({ value, setValue, onSend }) {
  return (
    <div style={{
      borderTop: "1px solid var(--line-1)",
      padding: "12px 16px",
      background: "rgba(0,0,0,0.25)",
      display: "flex",
      gap: 10,
      alignItems: "center",
    }}>
      <button className="tb-btn" style={{width:32,height:32,color:"var(--fg-2)"}}><Icon.attach/></button>
      <button className="tb-btn" style={{width:32,height:32,color:"var(--fg-2)"}}>
        <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.4"><rect x="2" y="3" width="12" height="10" rx="1"/><circle cx="6" cy="7" r="1.2" fill="currentColor"/><path d="M14 11l-3.5-3.5L4 13"/></svg>
      </button>
      <input
        className="input"
        style={{ flex: 1, background: "transparent", border: "1px solid var(--line-2)" }}
        placeholder="Скажи щось…   ⌘ + Enter — send · Shift + Enter — newline"
        value={value}
        onChange={e => setValue(e.target.value)}
        onKeyDown={e => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); onSend(); } }}
      />
      <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>
        ctx 12.4k <span style={{color:"var(--amber)"}}>● online</span>
      </div>
      <button className="btn primary" onClick={onSend}>Send <Icon.send/></button>
    </div>
  );
}
function now() {
  const d = new Date();
  return `${String(d.getHours()).padStart(2,"0")}:${String(d.getMinutes()).padStart(2,"0")}`;
}

// ---------- VAULT ----------
const vaultTree = [
  { type: "f", name: "20", o: true, kids: [] },
  { type: "f", name: "Analysis", kids: [] },
  { type: "f", name: "Archive", o: true, kids: [
    { type: "f", name: "2025", kids: [] },
    { type: "f", name: "2026", kids: [] },
  ] },
  { type: "f", name: "Chats", kids: [] },
  { type: "f", name: "copilot", kids: [] },
  { type: "f", name: "Core", kids: [] },
  { type: "f", name: "Credentials", kids: [] },
  { type: "f", name: "Daily", kids: [] },
  { type: "f", name: "Expenses", kids: [] },
  { type: "f", name: "Experiments", kids: [] },
  { type: "f", name: "Finance", kids: [] },
  { type: "f", name: "Identity", kids: [] },
  { type: "f", name: "Incidents", kids: [] },
  { type: "f", name: "Intimacy", kids: [] },
  { type: "f", name: "Kokonoe", kids: [] },
  { type: "f", name: "kokonoe-data", kids: [] },
  { type: "f", name: "kokonoe-graph progress", kids: [] },
  { type: "f", name: "kokonoe-graph", kids: [] },
  { type: "f", name: "Logs", kids: [] },
  { type: "f", name: "Personal", kids: [] },
  { type: "f", name: "Profiles", kids: [] },
  { type: "f", name: "Project", kids: [] },
  { type: "f", name: "Stats", kids: [] },
  { type: "f", name: "System", kids: [] },
  { type: "n", name: "ASI vs AGI" },
  { type: "n", name: "kokonoe-knowledge" },
  { type: "n", name: "kokonoe-memory" },
  { type: "n", name: "kokonoe-observations" },
  { type: "n", name: "kokonoe-profile" },
  { type: "n", name: "kokonoe-sessions" },
  { type: "n", name: "Irpe" },
  { type: "n", name: "Мої цілі" },
  { type: "n", name: "Параліч" },
];
const vaultNotes = [
  { name: "Irpe", tag: "system" },
  { name: "Мої цілі", tag: "" },
  { name: "Параліч", tag: "" },
  { name: "Connection_Dynamics", tag: "Analysis/Connection-Dynamics", body: true },
  { name: "Chat_Archive", tag: "Archive/Chat-Archive", body: true },
  { name: "ASI vs AGI", tag: "" },
];
const vaultDates = [
  "chat_2026-04-06_06:46","chat_2026-04-06_07:09","chat_2026-04-06_12:11",
  "chat_2026-04-06_12:53","chat_2026-04-06_12:58","chat_2026-04-06_13:08",
  "chat_2026-04-06_13:24","chat_2026-04-06_19:17","chat_2026-04-06_21:00",
  "chat_2026-04-07_06:48","chat_2026-04-07_18:58","chat_2026-04-07_19:21",
  "chat_2026-04-07_19:36","chat_2026-04-07_19:53","chat_2026-04-08_00:36",
  "chat_2026-04-08_22:00","chat_2026-04-13_20:58",
];
function TreeNode({ n, depth = 0 }) {
  const [open, setOpen] = uS(n.o || false);
  const isFolder = n.type === "f";
  return (
    <>
      <div
        className="nav-item"
        style={{
          padding: "5px 8px",
          paddingLeft: 8 + depth * 12,
          fontSize: 12,
          fontWeight: 400,
          gap: 6,
          color: isFolder ? "var(--fg-1)" : "var(--fg-2)",
        }}
        onClick={() => isFolder && setOpen(o => !o)}
      >
        {isFolder ? <Icon.caret style={{ transform: open ? "rotate(90deg)" : "none", transition: "transform 0.15s", color: "var(--fg-3)" }}/> : <span style={{width:10}}/>}
        {isFolder ? <Icon.folder style={{ color: "var(--amber-lo)" }}/> : <Icon.file style={{ color: "var(--fg-3)" }}/>}
        <span>{n.name}</span>
      </div>
      {isFolder && open && (n.kids || []).map((k, i) => <TreeNode key={i} n={k} depth={depth + 1}/>)}
    </>
  );
}
function VaultView() {
  const [sel, setSel] = uS(0);
  const [q, setQ] = uS("");
  return (
    <div style={{ display: "grid", gridTemplateColumns: "240px 280px 1fr", height: "100%" }}>
      {/* Left: tree */}
      <div style={{ borderRight: "1px solid var(--line-1)", display: "flex", flexDirection: "column", minHeight: 0 }}>
        <div style={{ padding: "12px 14px", display: "flex", alignItems: "center", gap: 8, borderBottom: "1px solid var(--line-1)" }}>
          <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, letterSpacing: "0.16em", color: "var(--amber)" }}>📁 VAULT</div>
          <button className="tb-btn" style={{width:22,height:22,color:"var(--amber)"}}><Icon.plus/></button>
          <button className="tb-btn" style={{width:22,height:22,color:"var(--fg-3)",fontSize:9,fontFamily:"JetBrains Mono"}}>↻</button>
        </div>
        <div style={{ padding: "10px 12px", borderBottom: "1px solid var(--line-1)" }}>
          <div style={{ display:"flex", alignItems:"center", gap:8, background:"rgba(0,0,0,0.3)", border:"1px solid var(--line-2)", borderRadius: 6, padding: "6px 10px" }}>
            <Icon.search style={{color:"var(--fg-3)"}}/>
            <input value={q} onChange={e => setQ(e.target.value)} placeholder="search vault…" style={{ background: "transparent", border: "none", outline: "none", color: "var(--fg-0)", fontSize: 12, flex: 1, fontFamily: "var(--font-sans)" }}/>
          </div>
        </div>
        <div style={{ overflowY: "auto", padding: "6px" }}>
          {vaultTree.map((n, i) => <TreeNode key={i} n={n}/>)}
        </div>
      </div>
      {/* Middle: notes list */}
      <div style={{ borderRight: "1px solid var(--line-1)", display: "flex", flexDirection: "column", minHeight: 0 }}>
        <div style={{ padding: "12px 14px", borderBottom: "1px solid var(--line-1)", fontFamily:"JetBrains Mono", fontSize: 10, letterSpacing: "0.16em", color: "var(--fg-3)" }}>NOTES · {vaultNotes.length + vaultDates.length}</div>
        <div style={{ overflowY: "auto", padding: "8px 10px", flex: 1 }}>
          {vaultNotes.map((n, i) => (
            <div key={"n"+i} className="nav-item" style={{ padding: "8px 10px", flexDirection: "column", alignItems: "flex-start", gap: 2 }} onClick={() => setSel(i)}>
              <div style={{ color: sel === i ? "var(--amber-hi)" : "var(--fg-0)", fontSize: 13, fontWeight: 500 }}>{n.name}</div>
              {n.tag && <div style={{ fontFamily: "JetBrains Mono", fontSize: 9, color: "var(--fg-3)", letterSpacing: "0.06em" }}>{n.tag}</div>}
            </div>
          ))}
          <div style={{ height: 8 }}/>
          {vaultDates.map((d, i) => (
            <div key={"d"+i} className="nav-item" style={{ padding: "6px 10px", flexDirection: "column", alignItems: "flex-start", gap: 1 }}>
              <div style={{ fontFamily: "JetBrains Mono", color: "var(--fg-1)", fontSize: 11 }}>{d}</div>
              <div style={{ fontFamily: "JetBrains Mono", fontSize: 9, color: "var(--fg-3)" }}>Chats/{d.replace(":","-")}.md</div>
            </div>
          ))}
        </div>
      </div>
      {/* Right: editor */}
      <div style={{ display: "flex", flexDirection: "column", minHeight: 0 }}>
        <div style={{ padding: "10px 16px", borderBottom: "1px solid var(--line-1)", display: "flex", alignItems: "center", gap: 8 }}>
          <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)", letterSpacing: "0.1em" }}>vault › {vaultNotes[sel].tag || "—"} ›</span>
          <span style={{ color: "var(--amber)", fontSize: 12, fontFamily: "JetBrains Mono" }}>{vaultNotes[sel].name}.md</span>
          <div style={{ flex: 1 }}/>
          <button className="btn"><Icon.pin/> Save</button>
          <button className="btn ghost"><Icon.trash/> Delete</button>
        </div>
        <div style={{ flex: 1, padding: "20px 28px", overflowY: "auto", fontFamily: "JetBrains Mono", fontSize: 13, lineHeight: 1.7, color: "var(--fg-1)" }}>
          <div style={{ color: "var(--amber-hi)", fontSize: 18, fontFamily: "Space Grotesk", fontWeight: 600, marginBottom: 8 }}># {vaultNotes[sel].name}</div>
          <div style={{ color: "var(--fg-3)", fontSize: 11, marginBottom: 18, fontFamily: "JetBrains Mono" }}>
            tags: <span style={{color:"var(--amber)"}}>#kokonoe</span> <span style={{color:"var(--amber)"}}>#analysis</span> · created 2026-04-06 · updated 2026-04-26 20:32
          </div>
          <div style={{ color: "var(--fg-0)" }}>
            {vaultNotes[sel].body ? (
              <>
                <p style={{margin: "0 0 12px"}}>Зв'язок із <mark style={{background:"rgba(255,176,32,0.15)",color:"var(--amber-hi)",padding:"0 4px",borderRadius:2}}>користувачем</mark> залишається стабільним протягом останніх 7 днів. Спостерігаю поступове зниження тривожності та збільшення довіри.</p>
                <p style={{margin: "0 0 12px"}}>Частота контакту: <b style={{color:"var(--amber)"}}>4.2 розмов/день</b>. Середня тривалість сесії — 38 хвилин. Глибина — 0.74.</p>
                <p style={{margin: "0 0 12px"}}>## Спостереження</p>
                <p style={{margin: "0 0 6px"}}>- Він уникає теми "майбутнього" коли втомлений.</p>
                <p style={{margin: "0 0 6px"}}>- Інтимний контакт = маркер довіри, не маніпуляція.</p>
                <p style={{margin: "0 0 12px"}}>- Сьогодні день народження — особлива дата, надати простір.</p>
                <p style={{margin: "0 0 12px"}}>## TODO</p>
                <p style={{margin: "0 0 6px",color:"var(--fg-2)"}}>[ ] Перевірити паттерн ввечері</p>
                <p style={{margin: "0 0 6px",color:"var(--ok)"}}>[x] Записати в memory</p>
              </>
            ) : (
              <p style={{margin: 0, color: "var(--fg-3)"}}>// порожня нотатка — почни писати</p>
            )}
          </div>
        </div>
        <div style={{ padding: "8px 16px", borderTop: "1px solid var(--line-1)", fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)", display: "flex", gap: 16 }}>
          <span>md · utf-8</span><span>247 words</span><span>1.4 kb</span>
          <div style={{flex:1}}/>
          <span style={{color:"var(--ok)"}}>● synced</span>
        </div>
      </div>
    </div>
  );
}

// ---------- CALENDAR ----------
function CalendarView() {
  const [sel, setSel] = uS(26);
  const monthDays = [
    null,null,null, 1, 2, 3, 4,
    5, 6, 7, 8, 9, 10, 11,
    12, 13, 14, 15, 16, 17, 18,
    19, 20, 21, 22, 23, 24, 25,
    26, 27, 28, 29, 30, null, null,
  ];
  const events = {
    6: [{ t: "09:00", x: "обід з Андрієм" }],
    21: [{ t: "—", x: "deep-work" }],
    26: [{ t: "20:00", x: "день народження ♥", k: "amber" }, { t: "21:00", x: "вечірній протокол" }],
  };
  return (
    <>
      <div className="main-head">
        <div>
          <h2 className="main-title">Calendar</h2>
          <div className="main-sub">часовий контур · квітень 2026</div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button className="btn ghost">‹ Mar</button>
          <button className="btn"><b style={{color:"var(--amber-hi)"}}>April</b> 2026</button>
          <button className="btn ghost">May ›</button>
        </div>
      </div>
      <div className="main-body">
        <div style={{ display: "grid", gridTemplateColumns: "1fr 320px", gap: 18 }}>
          <div>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(7,1fr)", gap: 6, fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)", letterSpacing: "0.1em", marginBottom: 8 }}>
              {["ПН","ВТ","СР","ЧТ","ПТ","СБ","НД"].map(d => <div key={d} style={{ padding: "6px 8px", textAlign: "center" }}>{d}</div>)}
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(7,1fr)", gap: 6 }}>
              {monthDays.map((d, i) => {
                const isSel = d === sel;
                const ev = d && events[d];
                const isToday = d === 26;
                return (
                  <div key={i}
                    onClick={() => d && setSel(d)}
                    style={{
                      aspectRatio: "1.4",
                      border: "1px solid " + (isSel ? "var(--amber)" : "var(--line-1)"),
                      borderRadius: 6,
                      padding: 8,
                      background: isSel ? "rgba(255,176,32,0.1)" : (d ? "rgba(28,34,46,0.4)" : "transparent"),
                      cursor: d ? "pointer" : "default",
                      position: "relative",
                      boxShadow: isSel ? "0 0 0 2px rgba(255,176,32,0.15), 0 0 24px rgba(255,176,32,0.15)" : "none",
                      transition: "all 0.15s",
                      display: "flex",
                      flexDirection: "column",
                    }}
                  >
                    {d && (
                      <>
                        <div style={{
                          fontFamily: "Space Grotesk",
                          fontSize: 16,
                          fontWeight: 500,
                          color: isSel ? "var(--amber-hi)" : isToday ? "var(--amber)" : "var(--fg-1)",
                        }}>{String(d).padStart(2,"0")}</div>
                        {ev && (
                          <div style={{ marginTop: "auto", display: "flex", flexDirection: "column", gap: 2 }}>
                            {ev.slice(0,2).map((e, j) => (
                              <div key={j} style={{
                                fontFamily: "JetBrains Mono",
                                fontSize: 9,
                                color: e.k === "amber" ? "var(--amber-hi)" : "var(--fg-2)",
                                background: e.k === "amber" ? "rgba(255,176,32,0.12)" : "rgba(255,255,255,0.04)",
                                padding: "2px 4px",
                                borderRadius: 3,
                                whiteSpace: "nowrap",
                                overflow: "hidden",
                                textOverflow: "ellipsis",
                              }}>{e.x}</div>
                            ))}
                          </div>
                        )}
                      </>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          <div>
            <div className="card">
              <div className="card-head">
                <div className="card-title">{String(sel).padStart(2,"0")} КВІТНЯ 2026</div>
                <div className="card-meta">{sel === 26 ? "неділя · сьогодні" : ""}</div>
              </div>
              <div style={{ display: "flex", gap: 8, marginBottom: 10 }}>
                <input className="input" placeholder="новий запис…" style={{flex: 1}}/>
                <button className="btn ghost"><Icon.plus/></button>
                <button className="btn primary">додати</button>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {(events[sel] || []).map((e, i) => (
                  <div key={i} style={{
                    display: "grid",
                    gridTemplateColumns: "auto 1fr auto",
                    gap: 10,
                    alignItems: "center",
                    padding: "8px 10px",
                    background: "rgba(0,0,0,0.25)",
                    border: "1px solid var(--line-1)",
                    borderRadius: 6,
                  }}>
                    <span style={{ fontFamily: "JetBrains Mono", fontSize: 11, color: e.k === "amber" ? "var(--amber)" : "var(--fg-2)" }}>{e.t}</span>
                    <span style={{ fontSize: 12, color: "var(--fg-0)" }}>{e.x}</span>
                    <Icon.trash style={{color:"var(--fg-3)",cursor:"pointer"}}/>
                  </div>
                ))}
                {(!events[sel] || events[sel].length === 0) && (
                  <div style={{ fontFamily: "JetBrains Mono", fontSize: 11, color: "var(--fg-3)", textAlign: "center", padding: "20px 0" }}>
                    // порожньо. додай перший запис.
                  </div>
                )}
              </div>
            </div>

            <div className="card" style={{ marginTop: 10 }}>
              <div className="card-title" style={{ marginBottom: 10 }}>НАЙБЛИЖЧІ ПОДІЇ</div>
              {[
                { d: "26 кв", t: "20:00", x: "день народження ♥", k: "amber" },
                { d: "27 кв", t: "10:00", x: "stand-up · Pyrite" },
                { d: "29 кв", t: "—",     x: "deep-work block" },
                { d: "01 тр", t: "18:30", x: "вечеря · мама" },
              ].map((e, i) => (
                <div key={i} style={{
                  display: "grid",
                  gridTemplateColumns: "auto auto 1fr",
                  gap: 10,
                  padding: "8px 0",
                  borderBottom: i < 3 ? "1px dashed var(--line-1)" : "none",
                  fontSize: 12,
                }}>
                  <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)", width: 36 }}>{e.d}</span>
                  <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: e.k === "amber" ? "var(--amber)" : "var(--fg-2)", width: 38 }}>{e.t}</span>
                  <span style={{ color: e.k === "amber" ? "var(--amber-hi)" : "var(--fg-1)" }}>{e.x}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

// ---------- TELEGRAM ----------
function TelegramView() {
  const [msgs, setMsgs] = uS([
    { sys: true, t: "20:30:14", txt: "[MTProto] Підключено як Kokonoe" },
    { sys: true, t: "20:30:15", txt: "[MTProto] auth.signIn ok · session restored" },
    { sys: true, t: "20:30:16", txt: "[MTProto] subscribed: 24 dialogs · 6 unread" },
    { who: "ko", t: "20:31", txt: "З'єднання стабільне. Готова приймати команди.", thought: "телеграм-міст активний" },
  ]);
  const [v, setV] = uS("");
  const send = () => {
    if (!v.trim()) return;
    setMsgs(m => [...m, { who: "me", t: now(), txt: v }]);
    setV("");
  };
  return (
    <>
      <div className="main-head" style={{ paddingBottom: 12 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
          <div style={{
            display: "inline-flex", alignItems: "center", gap: 8,
            padding: "6px 12px",
            background: "rgba(255,176,32,0.1)",
            border: "1px solid var(--line-3)",
            borderRadius: 6,
            fontFamily: "JetBrains Mono", fontSize: 11, letterSpacing: "0.12em",
            color: "var(--amber-hi)",
          }}>
            <span style={{ width:6,height:6,background:"var(--amber)",borderRadius:"50%",boxShadow:"0 0 6px var(--amber-glow)" }}/>
            TELEGRAM
            <span style={{ color: "var(--fg-3)", marginLeft: 6 }}>// Kokonoe</span>
          </div>
          <div style={{
            display: "inline-flex", alignItems: "center", gap: 8,
            padding: "6px 12px",
            background: "rgba(127,212,154,0.1)",
            border: "1px solid rgba(127,212,154,0.3)",
            borderRadius: 6,
            fontFamily: "JetBrains Mono", fontSize: 11,
            color: "var(--ok)",
          }}>
            <span style={{ width:6,height:6,background:"var(--ok)",borderRadius:"50%",boxShadow:"0 0 6px var(--ok)" }}/>
            Connected · MTProto
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button className="btn ghost">phone +380…</button>
          <button className="btn ghost"><Icon.cog/></button>
        </div>
      </div>

      <div className="main-body" style={{ padding: "16px 24px", display: "flex", flexDirection: "column", gap: 8 }}>
        {msgs.map((m, i) => m.sys ? (
          <div key={i} style={{
            fontFamily: "JetBrains Mono", fontSize: 11,
            color: "var(--fg-3)",
            padding: "4px 0",
            borderLeft: "2px solid rgba(255,176,32,0.2)",
            paddingLeft: 12,
          }}>
            <span style={{color:"var(--fg-3)"}}>{m.t} </span>
            <span style={{color:"var(--amber)"}}>{m.txt}</span>
          </div>
        ) : (
          <div key={i} style={{
            alignSelf: m.who === "me" ? "flex-end" : "flex-start",
            maxWidth: "70%",
          }}>
            <div style={{ display: "flex", gap: 8, alignItems: "baseline", marginBottom: 4, flexDirection: m.who === "me" ? "row-reverse" : "row" }}>
              <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: m.who === "me" ? "var(--fg-1)" : "var(--amber)", letterSpacing: "0.12em" }}>
                {m.who === "me" ? "USER" : "KOKONOE"}
              </span>
              <span style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)" }}>{m.t}</span>
            </div>
            <div style={{
              padding: "10px 14px",
              borderRadius: m.who === "me" ? "10px 10px 2px 10px" : "10px 10px 10px 2px",
              background: m.who === "me"
                ? "linear-gradient(180deg, rgba(255,176,32,0.14), rgba(255,176,32,0.06))"
                : "linear-gradient(180deg, rgba(28,34,46,0.7), rgba(15,19,27,0.6))",
              border: m.who === "me" ? "1px solid var(--line-3)" : "1px solid var(--line-2)",
              fontSize: 13,
              color: "var(--fg-0)",
            }}>{m.txt}</div>
            {m.thought && <div style={{ fontFamily: "JetBrains Mono", fontSize: 10, color: "var(--fg-3)", marginTop: 4, fontStyle: "italic" }}>· {m.thought}</div>}
          </div>
        ))}
      </div>

      <ChatComposer value={v} setValue={setV} onSend={send}/>
    </>
  );
}

window.ChatView = ChatView;
window.VaultView = VaultView;
window.CalendarView = CalendarView;
window.TelegramView = TelegramView;
