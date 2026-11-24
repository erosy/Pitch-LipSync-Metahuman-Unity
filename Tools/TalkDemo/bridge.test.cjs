const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('Assets/WebGLTemplates/TalkDemo/bridge.js', 'utf8');
function setup() {
  const sent = [], calls = [], listeners = {};
  const parent = { postMessage: (data, origin) => sent.push({ data, origin }) };
  const button = { addEventListener: (name, callback) => { listeners.click = callback; } };
  const window = { parent, TalkDemoConfig: { allowedParentOrigins: ['https://host.test'] },
    document: { referrer: 'https://host.test/page', getElementById: () => button },
    addEventListener: (name, callback) => { listeners[name] = callback; } };
  vm.runInNewContext(source, { window, URL });
  return { window, sent, calls, listeners, button, parent,
    attach: () => window.TalkDemoTransport.attach({ SendMessage: (...args) => calls.push(args) }),
    message: (data, origin = 'https://host.test', from = parent) => listeners.message({ data, origin, source: from }) };
}
test('engine events wait until Unity instance attaches', () => {
  const s = setup(); s.window.TalkDemoTransport.publish({ ns: 'metahuman-talk', event_name: 'bridge-ready' });
  assert.equal(s.sent.length, 0); s.attach(); assert.equal(s.sent[0].origin, 'https://host.test');
});
test('only configured origin and parent may send commands', () => {
  const s = setup(); s.attach(); s.window.TalkDemoTransport.publish({ event_name: 'bridge-ready' });
  const cmd = { ns: 'metahuman-talk', cmd: 'stop' };
  s.message(cmd, 'https://untrusted.test'); s.message(cmd, 'https://host.test', {}); assert.equal(s.calls.length, 0);
  s.message(cmd); assert.equal(JSON.parse(s.calls[0][2]).cmd, 'stop');
});
test('commands cannot enter Unity before bridge readiness', () => {
  const s = setup(); s.attach(); s.message({ ns: 'metahuman-talk', cmd: 'stop' });
  assert.equal(s.calls.length, 0); assert.equal(s.sent[0].data.event_name, 'error');
});
test('speech objects are forwarded as JSON without persisting audio', () => {
  const s = setup(); s.attach(); s.window.TalkDemoTransport.publish({ event_name: 'bridge-ready' });
  const payload = { persona_key: 'Ethan Reeve', audio_base64: 'UklGRg==' };
  s.message({ ns: 'metahuman-talk', cmd: 'speak', payload });
  assert.deepEqual(JSON.parse(JSON.parse(s.calls[0][2]).speech_json), payload);
});
test('audio button resumes the actual iframe audio context', async () => {
  const s = setup(); let resumed = false;
  s.window.TalkDemoAudio = { resume: async () => { resumed = true; }, ready: () => resumed };
  await s.listeners.click(); assert.equal(s.button.hidden, true); assert.equal(s.sent[0].data.event_name, 'audio-enabled');
});
test('Unity template loader and native plugin have valid JavaScript syntax', () => {
  const html = fs.readFileSync('Assets/WebGLTemplates/TalkDemo/index.html', 'utf8');
  const script = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)][0][1]
    .replace(/#if[^\n]*\n[\s\S]*?#endif/g, '')
    .replace(/\{\{\{ JSON.stringify\(\w+\) \}\}\}/g, '"Talk Demo"')
    .replace(/\{\{\{[^}]+\}\}\}/g, 'placeholder');
  new vm.Script(script);
  new vm.Script(fs.readFileSync('Assets/Plugins/WebGL/TalkDemo.jslib', 'utf8'));
});
