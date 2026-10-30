const shown = new Map();
const root = document.getElementById('devices');
const connection = document.getElementById('connection');

function addDevice() {
  const section = document.createElement('section');
  const heading = document.createElement('h2');
  const status = document.createElement('pre');
  const eyes = document.createElement('div');
  eyes.className = 'eyes';
  section.append(heading, status, eyes);
  root.append(section);
  return { section, heading, status, eyes, urls: '', wasStreaming: false };
}

async function refresh() {
  try {
    const response = await fetch('/devices', { cache: 'no-store', signal: AbortSignal.timeout(5000) });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const devices = await response.json();
    connection.textContent = devices.length ? 'Camera server connected.' : 'Waiting for an EyeChip. Check headset power, USB, and the console log.';
    const present = new Set(devices.map(d => d.DeviceId));
    for (const [id, entry] of shown) if (!present.has(id)) { entry.section.remove(); shown.delete(id); }
    for (const device of devices) {
      let entry = shown.get(device.DeviceId);
      if (!entry) { entry = addDevice(); shown.set(device.DeviceId, entry); }
      entry.heading.textContent = `${device.DeviceModel} ${device.SerialNumber}`;
      entry.status.textContent = `${device.Stage}\nLeft frames: ${device.LeftFrames} | Right frames: ${device.RightFrames}${device.Error ? `\n${device.Error}` : ''}`;
      const urls = `${device.LeftUrl}|${device.RightUrl}`;
      if (entry.urls !== urls) {
        entry.urls = urls;
        entry.eyes.replaceChildren();
        for (const eye of ['Left', 'Right']) {
          const url = device[`${eye}Url`];
          if (!url) continue;
          const figure = document.createElement('figure');
          const link = document.createElement('a');
          link.href = url; link.textContent = `${eye} stream`;
          const image = document.createElement('img');
          image.alt = `${eye} eye camera`; image.src = url;
          image.addEventListener('error', () => { image.dataset.retry = 'true'; });
          figure.append(link, image); entry.eyes.append(figure);
        }
      }
      if (device.IsStreaming) for (const image of entry.eyes.querySelectorAll(entry.wasStreaming ? 'img[data-retry]' : 'img')) {
        delete image.dataset.retry;
        image.src = new URL(image.src).pathname + `?retry=${Date.now()}`;
      }
      entry.wasStreaming = device.IsStreaming;
    }
  } catch (error) { connection.textContent = `Camera server unavailable: ${error.message}. Retrying…`; }
  setTimeout(refresh, 2000);
}
refresh();
