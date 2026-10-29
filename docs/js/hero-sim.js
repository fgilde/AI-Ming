/* Hero demo: a tiny simulation of what PowerAim does — detection boxes, an FOV circle, an aim point
   that leads the target, and a trigger that fires once the crosshair is on the head.
   Hover to take the crosshair; hold the mouse button to act as the aim key. */
(function () {
  "use strict";
  var canvas = document.getElementById("sim");
  if (!canvas) return;
  var ctx = canvas.getContext("2d");
  var reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var W = 0, H = 0, dpr = 1;
  var opts = { assist: true, aimpoint: true, esp: true };
  var C = { cyan: "#60cdff", purple: "#a87bf0", orange: "#ff8c00", ink: "#ece9f7", red: "#ff5577" };

  function resize() {
    var r = canvas.getBoundingClientRect();
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    W = r.width; H = r.height;
    canvas.width = Math.round(W * dpr); canvas.height = Math.round(H * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  // ---- world ----
  var horizon = 0.42;
  function rand(a, b) { return a + Math.random() * (b - a); }
  var targets = [], nextId = 1;
  function spawn(t) {
    t = t || {};
    t.id = nextId++;
    t.z = rand(0.25, 1);                       // depth: 1 = close
    t.x = rand(0.12, 0.88);
    t.vx = rand(0.03, 0.09) * (Math.random() < 0.5 ? -1 : 1);
    t.vz = rand(-0.05, 0.05);
    t.phase = rand(0, 6.28);
    t.hit = 0; t.conf = rand(0.86, 0.98);
    return t;
  }
  for (var i = 0; i < 3; i++) targets.push(spawn());

  function geom(t) {
    var s = 70 + t.z * 170;                    // body height in px at 900px width
    s *= W / 900;
    var groundY = H * (horizon + (1 - horizon) * (0.18 + t.z * 0.62));
    var bob = Math.sin(t.phase) * s * 0.02;
    var cx = t.x * W;
    var h = s, w = s * 0.42;
    return { cx: cx, top: groundY - h + bob, bottom: groundY + bob, w: w, h: h, head: { x: cx, y: groundY - h * 0.88 + bob, r: h * 0.09 } };
  }

  // ---- crosshair / control ----
  var cross = { x: 0, y: 0, vx: 0, vy: 0 };
  var hover = false, keyDown = false, pending = { dx: 0, dy: 0 };
  var auto = { t: 0, engaged: false, wander: { x: 0.5, y: 0.5 } };
  var locked = null, shots = 0, hits = 0, flash = 0, hitMarks = [];
  var lastShot = 0, onTargetMs = 0;

  function engaged() { return opts.assist && (hover ? keyDown : auto.engaged); }

  function pickTarget() {
    var fov = W * 0.2, best = null, bestD = 1e9;
    targets.forEach(function (t) {
      if (t.hit) return;
      var g = geom(t), d = Math.hypot(g.head.x - cross.x, g.head.y - cross.y);
      // stay on the current target while it is inside the sticky radius
      if (locked === t && d < fov * 1.3) d *= 0.4;
      if (d < fov && d < bestD) { best = t; bestD = d; }
    });
    return best;
  }

  function step(dt) {
    var s = dt / 1000;
    targets.forEach(function (t) {
      t.phase += s * 9;
      if (t.hit) { t.hit -= dt; if (t.hit <= 0) spawn(t); return; }
      t.x += t.vx * s; t.z = Math.min(1, Math.max(0.2, t.z + t.vz * s));
      if (t.x < 0.08 || t.x > 0.92) t.vx *= -1;
      if (t.z <= 0.2 || t.z >= 1) t.vz *= -1;
      if (Math.random() < s * 0.25) t.vx = rand(0.03, 0.1) * (Math.random() < 0.5 ? -1 : 1);
      t.conf += (rand(0.84, 0.99) - t.conf) * 0.05;
    });

    // the user's own movement, or the demo's when nobody hovers
    if (hover) {
      cross.x += pending.dx; cross.y += pending.dy; pending.dx = pending.dy = 0;
    } else {
      auto.t += dt;
      if (auto.t > (auto.engaged ? 2200 : 1400)) { auto.t = 0; auto.engaged = !auto.engaged; auto.wander = { x: rand(0.2, 0.8), y: rand(0.35, 0.7) }; }
      cross.x += (auto.wander.x * W - cross.x) * Math.min(1, s * 1.6);
      cross.y += (auto.wander.y * H - cross.y) * Math.min(1, s * 1.6);
    }

    locked = engaged() ? pickTarget() : null;
    if (locked) {
      // lead the target by the latency, then close a share of the gap per frame (smoothed)
      var g = geom(locked), lead = locked.vx * W * 0.12;
      var ax = g.head.x + lead, ay = g.head.y;
      var k = 1 - Math.exp(-s * 9);
      cross.x += (ax - cross.x) * k; cross.y += (ay - cross.y) * k;
      // trigger: fire after a short dwell on the head
      var on = Math.hypot(g.head.x - cross.x, g.head.y - cross.y) < g.head.r * 1.4;
      onTargetMs = on ? onTargetMs + dt : 0;
      if (on && onTargetMs > 90 && performance.now() - lastShot > 380) {
        lastShot = performance.now(); shots++; hits++; flash = 1;
        hitMarks.push({ x: g.head.x, y: g.head.y, life: 1 });
        locked.hit = 900;
      }
    } else onTargetMs = 0;
    cross.x = Math.max(0, Math.min(W, cross.x)); cross.y = Math.max(0, Math.min(H, cross.y));
    flash = Math.max(0, flash - s * 4);
    hitMarks.forEach(function (m) { m.life -= s * 1.6; });
    hitMarks = hitMarks.filter(function (m) { return m.life > 0; });
  }

  // ---- drawing ----
  function drawScene(time) {
    var hy = H * horizon;
    var sky = ctx.createLinearGradient(0, 0, 0, hy);
    sky.addColorStop(0, "#0b0618"); sky.addColorStop(1, "#1d1036");
    ctx.fillStyle = sky; ctx.fillRect(0, 0, W, hy);
    var floor = ctx.createLinearGradient(0, hy, 0, H);
    floor.addColorStop(0, "#140b26"); floor.addColorStop(1, "#06040c");
    ctx.fillStyle = floor; ctx.fillRect(0, hy, W, H - hy);
    // sun / glow on the horizon
    var glow = ctx.createRadialGradient(W * 0.5, hy, 0, W * 0.5, hy, W * 0.45);
    glow.addColorStop(0, "rgba(168,123,240,.35)"); glow.addColorStop(1, "rgba(168,123,240,0)");
    ctx.fillStyle = glow; ctx.fillRect(0, 0, W, H);
    // skyline blocks
    ctx.fillStyle = "#0a0614";
    for (var i = 0; i < 14; i++) {
      var bw = W / 14, bh = (Math.sin(i * 12.9898) * 0.5 + 0.5) * hy * 0.35 + hy * 0.08;
      ctx.fillRect(i * bw, hy - bh, bw - 2, bh);
    }
    // perspective floor grid, scrolling toward the viewer
    ctx.strokeStyle = "rgba(96,205,255,.14)"; ctx.lineWidth = 1;
    ctx.beginPath();
    for (var x = -10; x <= 10; x++) { ctx.moveTo(W / 2 + x * W * 0.02, hy); ctx.lineTo(W / 2 + x * W * 0.16, H); }
    var off = reduced ? 0 : (time / 1600) % 1;
    for (var r = 0; r < 12; r++) { var p = Math.pow((r + off) / 12, 2.2), y = hy + p * (H - hy); ctx.moveTo(0, y); ctx.lineTo(W, y); }
    ctx.stroke();
  }

  function drawTarget(t) {
    var g = geom(t);
    var a = t.hit ? Math.max(0, t.hit / 900) : 1;
    ctx.save(); ctx.globalAlpha = a;
    ctx.fillStyle = t.hit ? "rgba(255,85,119,.55)" : "#120a22";
    ctx.strokeStyle = t.hit ? C.red : "rgba(168,123,240,.55)"; ctx.lineWidth = 1.2;
    var w = g.w, h = g.h, x = g.cx;
    ctx.beginPath(); ctx.arc(g.head.x, g.head.y, g.head.r, 0, 6.283); ctx.fill(); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(x - w * 0.42, g.top + h * 0.24); ctx.lineTo(x + w * 0.42, g.top + h * 0.24);
    ctx.lineTo(x + w * 0.32, g.top + h * 0.62); ctx.lineTo(x + w * 0.26, g.bottom);
    ctx.lineTo(x + w * 0.06, g.bottom); ctx.lineTo(x, g.top + h * 0.68); ctx.lineTo(x - w * 0.06, g.bottom);
    ctx.lineTo(x - w * 0.26, g.bottom); ctx.lineTo(x - w * 0.32, g.top + h * 0.62); ctx.closePath();
    ctx.fill(); ctx.stroke();
    ctx.restore();
    if (t.hit || !opts.esp) return;
    // ESP: corner-bracket box, label, tracer
    var bx = x - w * 0.6, by = g.top - h * 0.04, bw = w * 1.2, bh = h * 1.08, c = Math.min(bw, bh) * 0.22;
    var hot = t === locked;
    ctx.strokeStyle = hot ? C.orange : C.cyan; ctx.lineWidth = hot ? 2 : 1.5;
    ctx.beginPath();
    [[bx, by, 1, 1], [bx + bw, by, -1, 1], [bx, by + bh, 1, -1], [bx + bw, by + bh, -1, -1]].forEach(function (k) {
      ctx.moveTo(k[0], k[1] + c * k[3]); ctx.lineTo(k[0], k[1]); ctx.lineTo(k[0] + c * k[2], k[1]);
    });
    ctx.stroke();
    var label = "enemy " + t.conf.toFixed(2) + "  #" + t.id;
    ctx.font = "600 " + Math.max(9, W * 0.012) + "px ui-monospace,Consolas,monospace";
    var tw = ctx.measureText(label).width + 10, th = Math.max(14, W * 0.018);
    ctx.fillStyle = hot ? C.orange : C.cyan; ctx.fillRect(bx, by - th - 2, tw, th);
    ctx.fillStyle = "#0a0614"; ctx.fillText(label, bx + 5, by - 6);
    ctx.strokeStyle = "rgba(96,205,255,.22)"; ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(W / 2, H); ctx.lineTo(x, by + bh); ctx.stroke();
  }

  function drawCrosshair() {
    var x = cross.x, y = cross.y, fov = W * 0.2;
    ctx.save();
    ctx.setLineDash([5, 7]); ctx.strokeStyle = engaged() ? "rgba(168,123,240,.75)" : "rgba(168,123,240,.35)"; ctx.lineWidth = 1.3;
    ctx.beginPath(); ctx.arc(x, y, fov, 0, 6.283); ctx.stroke(); ctx.setLineDash([]);
    if (opts.aimpoint && locked) {
      // the point it steers at (+) and where it lands once the moves in flight show (ring)
      var g = geom(locked), ax = g.head.x + locked.vx * W * 0.12, ay = g.head.y;
      ctx.strokeStyle = C.cyan; ctx.lineWidth = 1.5;
      ctx.beginPath(); ctx.moveTo(ax - 7, ay); ctx.lineTo(ax + 7, ay); ctx.moveTo(ax, ay - 7); ctx.lineTo(ax, ay + 7); ctx.stroke();
      var lx = x + (ax - x) * 0.6, ly = y + (ay - y) * 0.6;
      ctx.strokeStyle = C.orange; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(lx, ly, 9, 0, 6.283); ctx.stroke();
      ctx.strokeStyle = "rgba(255,140,0,.35)"; ctx.setLineDash([3, 4]);
      ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(ax, ay); ctx.stroke(); ctx.setLineDash([]);
    }
    var col = flash > 0 ? C.red : C.ink;
    ctx.strokeStyle = col; ctx.lineWidth = 2; ctx.shadowColor = col; ctx.shadowBlur = 8;
    ctx.beginPath();
    ctx.moveTo(x - 14, y); ctx.lineTo(x - 5, y); ctx.moveTo(x + 5, y); ctx.lineTo(x + 14, y);
    ctx.moveTo(x, y - 14); ctx.lineTo(x, y - 5); ctx.moveTo(x, y + 5); ctx.lineTo(x, y + 14);
    ctx.stroke();
    ctx.restore();
    hitMarks.forEach(function (m) {
      ctx.save(); ctx.globalAlpha = m.life; ctx.strokeStyle = C.red; ctx.lineWidth = 2.5;
      var r = 8 + (1 - m.life) * 10;
      ctx.beginPath();
      ctx.moveTo(m.x - r, m.y - r); ctx.lineTo(m.x - r / 2, m.y - r / 2); ctx.moveTo(m.x + r, m.y - r); ctx.lineTo(m.x + r / 2, m.y - r / 2);
      ctx.moveTo(m.x - r, m.y + r); ctx.lineTo(m.x - r / 2, m.y + r / 2); ctx.moveTo(m.x + r, m.y + r); ctx.lineTo(m.x + r / 2, m.y + r / 2);
      ctx.stroke(); ctx.restore();
    });
  }

  var hud = { fps: document.getElementById("sim-fps"), inf: document.getElementById("sim-inf"), hits: document.getElementById("sim-hits"), state: document.getElementById("sim-state") };
  var fpsAcc = 0, fpsN = 0, hudT = 0;
  function updateHud(dt) {
    fpsAcc += dt; fpsN++; hudT += dt;
    if (hudT < 250) return;
    if (hud.fps) hud.fps.textContent = Math.round(1000 * fpsN / fpsAcc);
    if (hud.inf) hud.inf.textContent = rand(2.6, 3.4).toFixed(1);
    if (hud.hits) hud.hits.textContent = hits;
    if (hud.state) hud.state.textContent = !opts.assist ? "assist off" : locked ? "locked #" + locked.id : engaged() ? "searching" : hover ? "hold click to aim" : "demo";
    fpsAcc = fpsN = hudT = 0;
  }

  var last = 0, visible = true, raf = 0;
  function frame(now) {
    raf = 0;
    var dt = Math.min(50, now - (last || now)); last = now;
    step(dt);
    drawScene(now);
    targets.slice().sort(function (a, b) { return a.z - b.z; }).forEach(drawTarget);
    drawCrosshair();
    if (flash > 0) { ctx.fillStyle = "rgba(255,85,119," + flash * 0.08 + ")"; ctx.fillRect(0, 0, W, H); }
    updateHud(dt);
    if (visible && !reduced) raf = requestAnimationFrame(frame);
  }
  function kick() { if (!raf && visible) { last = 0; raf = requestAnimationFrame(frame); } }

  // ---- input ----
  canvas.addEventListener("pointerenter", function (e) {
    if (e.pointerType !== "mouse") return;
    hover = true;
    var r = canvas.getBoundingClientRect(); cross.x = e.clientX - r.left; cross.y = e.clientY - r.top;
  });
  canvas.addEventListener("pointerleave", function () { hover = false; keyDown = false; });
  canvas.addEventListener("pointermove", function (e) { if (hover) { pending.dx += e.movementX; pending.dy += e.movementY; } });
  canvas.addEventListener("pointerdown", function (e) { if (e.pointerType === "mouse") { keyDown = true; e.preventDefault(); } });
  window.addEventListener("pointerup", function () { keyDown = false; });
  canvas.addEventListener("contextmenu", function (e) { e.preventDefault(); });
  document.querySelectorAll("[data-sim]").forEach(function (b) {
    b.addEventListener("click", function () {
      var k = b.getAttribute("data-sim"); opts[k] = !opts[k];
      b.setAttribute("aria-pressed", String(opts[k]));
      if (reduced) frame(performance.now());
    });
  });

  if ("IntersectionObserver" in window) {
    new IntersectionObserver(function (es) { visible = es[0].isIntersecting; if (visible) kick(); }).observe(canvas);
  }
  window.addEventListener("resize", function () { resize(); if (reduced) frame(performance.now()); });
  resize();
  cross.x = W * 0.5; cross.y = H * 0.55;
  if (reduced) { auto.engaged = true; for (var n = 0; n < 40; n++) step(16); frame(performance.now()); }
  else kick();
})();
