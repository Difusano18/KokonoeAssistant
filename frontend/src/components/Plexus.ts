interface Particle {
  theta: number;
  phi: number;
  x: number;
  y: number;
  z: number;
  vTheta: number;
  vPhi: number;
}

interface Projected {
  sx: number;
  sy: number;
  scale: number;
}

const PARTICLE_COUNT = 120;
const RADIUS = 260;
const MAX_DISTANCE = 120;
const FOV = 600;

export function initPlexus(): void {
  const canvas = document.getElementById("plexus-canvas") as HTMLCanvasElement | null;
  const ctx = canvas?.getContext("2d") ?? null;
  if (!canvas || !ctx)
    return;

  const globalWindow = window as unknown as { plexusEnabled?: boolean };
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  globalWindow.plexusEnabled = !reduceMotion;
  const toggle = document.getElementById("plexus-enabled") as HTMLInputElement | null;
  if (toggle && reduceMotion)
    toggle.checked = false;

  let width = 0;
  let height = 0;
  let raf = 0;
  let rotX = 0;
  let rotY = 0;
  const particles: Particle[] = [];

  function resize(): void {
    width = canvas!.width = window.innerWidth;
    height = canvas!.height = window.innerHeight;
  }

  function init(): void {
    particles.length = 0;
    for (let i = 0; i < PARTICLE_COUNT; i++) {
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(2 * Math.random() - 1);
      particles.push({
        theta, phi,
        x: 0, y: 0, z: 0,
        vTheta: (Math.random() - 0.5) * 0.003,
        vPhi: (Math.random() - 0.5) * 0.002
      });
    }
  }

  function project(x: number, y: number, z: number): Projected {
    const cosY = Math.cos(rotY), sinY = Math.sin(rotY);
    const x2 = x * cosY - z * sinY;
    const z2 = x * sinY + z * cosY;
    const cosX = Math.cos(rotX), sinX = Math.sin(rotX);
    const y2 = y * cosX - z2 * sinX;
    const z3 = y * sinX + z2 * cosX;
    const scale = FOV / (FOV + z3 + 300);
    return { sx: width / 2 + x2 * scale, sy: height / 2 + y2 * scale, scale };
  }

  function draw(ts: number): void {
    if (!globalWindow.plexusEnabled) {
      raf = 0;
      return;
    }
    ctx!.clearRect(0, 0, width, height);
    rotX = Math.sin(ts * 0.0001) * 0.3;
    rotY = ts * 0.00008;

    const accent = getComputedStyle(document.documentElement).getPropertyValue("--accent").trim() || "#5fc1b3";

    const projected: Projected[] = particles.map(p => {
      p.theta += p.vTheta;
      p.phi += p.vPhi;
      const sinPhi = Math.sin(p.phi);
      p.x = RADIUS * sinPhi * Math.cos(p.theta);
      p.y = RADIUS * Math.cos(p.phi);
      p.z = RADIUS * sinPhi * Math.sin(p.theta);
      return project(p.x, p.y, p.z);
    });

    for (let i = 0; i < particles.length; i++) {
      const a = particles[i]!;
      for (let j = i + 1; j < particles.length; j++) {
        const b = particles[j]!;
        const dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
        const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);
        if (dist < MAX_DISTANCE) {
          const pa = projected[i]!, pb = projected[j]!;
          ctx!.beginPath();
          ctx!.moveTo(pa.sx, pa.sy);
          ctx!.lineTo(pb.sx, pb.sy);
          ctx!.strokeStyle = accent;
          ctx!.globalAlpha = (1 - dist / MAX_DISTANCE) * 0.6;
          ctx!.lineWidth = 0.5;
          ctx!.stroke();
        }
      }
    }

    ctx!.globalAlpha = 0.9;
    for (const { sx, sy, scale } of projected) {
      ctx!.beginPath();
      ctx!.arc(sx, sy, Math.max(0.8, 1.5 * scale), 0, Math.PI * 2);
      ctx!.fillStyle = accent;
      ctx!.fill();
    }

    ctx!.globalAlpha = 1;
    raf = requestAnimationFrame(draw);
  }

  window.addEventListener("resize", resize);
  resize();
  init();
  if (globalWindow.plexusEnabled)
    raf = requestAnimationFrame(draw);

  toggle?.addEventListener("change", () => {
    globalWindow.plexusEnabled = toggle.checked;
    if (globalWindow.plexusEnabled && !raf)
      raf = requestAnimationFrame(draw);
  });
}
