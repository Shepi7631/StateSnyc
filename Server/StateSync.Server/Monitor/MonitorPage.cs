namespace StateSync.Server.Monitor;

public static class MonitorPage
{
	public const string Html = """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>GM Monitor</title>
<style>
    body { margin: 0; background: #1a1a2e; color: #eee; font-family: monospace; display: flex; }
    #canvas { border: 1px solid #333; }
    #info { padding: 16px; width: 280px; overflow-y: auto; }
    .player { margin-bottom: 8px; padding: 8px; background: #16213e; border-radius: 4px; }
    .label { color: #aaa; font-size: 11px; }
    h3 { margin: 0 0 8px; color: #0f3460; }
</style>
</head>
<body>
<canvas id="canvas" width="600" height="600"></canvas>
<div id="info"><h3>GM Monitor</h3><div id="status">Loading...</div></div>
<script>
const canvas = document.getElementById('canvas');
const ctx = canvas.getContext('2d');
const infoDiv = document.getElementById('status');
let navmesh = null;
let roomId = 'room1';
let scale = 1;
let offsetX = 0, offsetZ = 0;

const colors = ['#e94560','#0f3460','#533483','#16c79a','#f5a623','#50d890','#6c5ce7','#fd79a8'];

async function loadNavmesh() {
    try {
        const resp = await fetch(`/api/rooms/${roomId}/navmesh`);
        if (resp.ok) navmesh = await resp.json();
        if (navmesh && navmesh.vertices.length > 0) {
            let minX=Infinity, maxX=-Infinity, minZ=Infinity, maxZ=-Infinity;
            for (const v of navmesh.vertices) {
                minX = Math.min(minX, v.x); maxX = Math.max(maxX, v.x);
                minZ = Math.min(minZ, v.z); maxZ = Math.max(maxZ, v.z);
            }
            const pad = 20;
            scale = Math.min((canvas.width - 2*pad) / (maxX - minX), (canvas.height - 2*pad) / (maxZ - minZ));
            offsetX = pad - minX * scale;
            offsetZ = pad - minZ * scale;
        }
    } catch(e) { console.error(e); }
}

function toScreen(x, z) {
    return [x * scale + offsetX, z * scale + offsetZ];
}

function drawNavmesh() {
    if (!navmesh) return;
    ctx.strokeStyle = '#333';
    ctx.lineWidth = 0.5;
    for (const tri of navmesh.triangles) {
        const [ax, az] = toScreen(navmesh.vertices[tri[0]].x, navmesh.vertices[tri[0]].z);
        const [bx, bz] = toScreen(navmesh.vertices[tri[1]].x, navmesh.vertices[tri[1]].z);
        const [cx, cz] = toScreen(navmesh.vertices[tri[2]].x, navmesh.vertices[tri[2]].z);
        ctx.beginPath();
        ctx.moveTo(ax, az); ctx.lineTo(bx, bz); ctx.lineTo(cx, cz); ctx.closePath();
        ctx.stroke();
    }
}

function drawPlayer(p, idx) {
    const color = colors[idx % colors.length];
    const [px, pz] = toScreen(p.position.x, p.position.z);

    if (p.path && p.path.length > 1) {
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.setLineDash([4, 4]);
        ctx.beginPath();
        const [sx, sz] = toScreen(p.path[0].x, p.path[0].z);
        ctx.moveTo(sx, sz);
        for (let i = 1; i < p.path.length; i++) {
            const [nx, nz] = toScreen(p.path[i].x, p.path[i].z);
            ctx.lineTo(nx, nz);
        }
        ctx.stroke();
        ctx.setLineDash([]);
    }

    if (p.isMoving) {
        const [tx, tz] = toScreen(p.target.x, p.target.z);
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(tx-5, tz); ctx.lineTo(tx+5, tz);
        ctx.moveTo(tx, tz-5); ctx.lineTo(tx, tz+5);
        ctx.stroke();
    }

    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(px, pz, 6, 0, Math.PI*2);
    ctx.fill();

    ctx.fillStyle = '#fff';
    ctx.font = '10px monospace';
    ctx.fillText(p.playerId.substring(0,6), px+8, pz+3);
}

async function update() {
    try {
        const resp = await fetch(`/api/rooms/${roomId}/state`);
        if (!resp.ok) { infoDiv.textContent = 'Room not found'; return; }
        const state = await resp.json();

        ctx.fillStyle = '#1a1a2e';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        drawNavmesh();

        state.players.forEach((p, i) => drawPlayer(p, i));

        let html = `<div class="label">Tick: ${state.tick} | Players: ${state.players.length}</div>`;
        state.players.forEach((p, i) => {
            html += `<div class="player" style="border-left:3px solid ${colors[i%colors.length]}">`;
            html += `<b>${p.playerId.substring(0,8)}</b><br>`;
            html += `<span class="label">Pos:</span> (${p.position.x.toFixed(1)}, ${p.position.z.toFixed(1)})<br>`;
            html += `<span class="label">Moving:</span> ${p.isMoving} | <span class="label">RTT:</span> ${p.rttMs}ms`;
            html += `</div>`;
        });
        infoDiv.innerHTML = html;
    } catch(e) { infoDiv.textContent = 'Error: ' + e.message; }
}

loadNavmesh().then(() => { update(); setInterval(update, 200); });
</script>
</body>
</html>
""";
}
