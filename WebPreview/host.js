'use strict';
const frame = document.getElementById('character');
const status = document.getElementById('status');
let origin = null, ready = false;
const send = command => {
  if (!ready) return;
  frame.contentWindow.postMessage({ ns: 'metahuman-talk', ...command }, origin);
};
document.getElementById('load').onclick = () => {
  const url = new URL(document.getElementById('build-url').value, location.href);
  if (!['http:', 'https:'].includes(url.protocol)) { status.textContent = 'Use an HTTP or HTTPS build URL.'; return; }
  origin = url.origin; ready = false;
  for (const id of ['send', 'select', 'stop']) document.getElementById(id).disabled = true;
  frame.src = url.href; status.textContent = 'Loading Unity…';
};
window.addEventListener('message', event => {
  if (event.source !== frame.contentWindow || event.origin !== origin || event.data?.ns !== 'metahuman-talk') return;
  const message = event.data;
  if (message.event_name === 'bridge-ready' && !ready) {
    ready = true;
    for (const id of ['send', 'select', 'stop']) document.getElementById(id).disabled = false;
    send({ cmd: 'select-persona', persona_key: document.getElementById('persona').value });
  }
  status.textContent = message.event_name + (message.persona_key ? ' — ' + message.persona_key : '') + (message.message ? ': ' + message.message : '');
  const log = document.getElementById('events');
  log.textContent = (JSON.stringify(message) + '\n' + log.textContent).slice(0, 12000);
});
document.getElementById('select').onclick = () => send({ cmd: 'select-persona', persona_key: document.getElementById('persona').value });
document.getElementById('stop').onclick = () => send({ cmd: 'stop' });
document.getElementById('send').onclick = () => {
  try { const payload = JSON.parse(document.getElementById('payload').value); send({ cmd: 'speak', request_id: payload.request_id || crypto.randomUUID(), payload }); }
  catch (error) { status.textContent = 'Invalid JSON: ' + error.message; }
};
document.getElementById('file').onchange = async event => {
  const file = event.target.files[0];
  if (!file) return;
  if (file.size > 22373717) { status.textContent = 'Payload file is too large.'; return; }
  document.getElementById('payload').value = await file.text();
};
let dark = false;
document.getElementById('background').onclick = () => {
  dark = !dark;
  document.getElementById('stage').style.background = dark ? 'linear-gradient(135deg,#18253c,#26384b)' : 'linear-gradient(135deg,#344c56,#c6b6a2 55%,#59453c)';
};
