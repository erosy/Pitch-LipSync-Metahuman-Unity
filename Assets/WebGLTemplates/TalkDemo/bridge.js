(function (window) {
  'use strict';
  const allowed = window.TalkDemoConfig.allowedParentOrigins;
  let parentOrigin = allowed[0], unity = null, bridgeReady = false;
  const events = [];
  try {
    const referrer = new URL(window.document.referrer).origin;
    if (allowed.includes(referrer)) parentOrigin = referrer;
  } catch (_) { /* A standalone page may have no referrer. */ }
  function emit(event) {
    if (window.parent !== window) window.parent.postMessage(event, parentOrigin);
  }
  function publish(json) {
    const event = typeof json === 'string' ? JSON.parse(json) : json;
    if (!unity) { events.push(event); return; }
    if (event.event_name === 'bridge-ready') bridgeReady = true;
    emit(event);
  }
  window.TalkDemoTransport = {
    publish,
    attach(instance) {
      unity = instance;
      while (events.length) publish(events.shift());
    }
  };
  window.addEventListener('message', function (event) {
    if (event.source !== window.parent || !allowed.includes(event.origin)) return;
    const msg = event.data;
    if (!msg || msg.ns !== 'metahuman-talk' || typeof msg.cmd !== 'string') return;
    parentOrigin = event.origin;
    if (msg.cmd === 'hello') {
      if (bridgeReady) emit({ ns: 'metahuman-talk', event_name: 'bridge-ready' });
      return;
    }
    if (!bridgeReady || !unity) {
      emit({ ns: 'metahuman-talk', event_name: 'error', message: 'Wait for bridge-ready.' });
      return;
    }
    if (!['select-persona', 'speak', 'stop'].includes(msg.cmd)) return;
    const command = { cmd: msg.cmd, request_id: typeof msg.request_id === 'string' ? msg.request_id : null };
    if (msg.cmd === 'select-persona') {
      if (typeof msg.persona_key !== 'string') return;
      command.persona_key = msg.persona_key;
    }
    if (msg.cmd === 'speak') {
      command.speech_json = typeof msg.payload === 'string' ? msg.payload : JSON.stringify(msg.payload);
      if (!command.speech_json || command.speech_json.length > 22373717) {
        emit({ ns: 'metahuman-talk', event_name: 'error', message: 'Missing or oversized speech payload.' });
        return;
      }
    }
    unity.SendMessage('Talk Demo', 'ReceiveCommand', JSON.stringify(command));
  });
  const audio = window.document.getElementById('enable-audio');
  audio.addEventListener('click', async function () {
    try {
      if (!window.TalkDemoAudio) throw new Error('Character is still loading. Try again shortly.');
      await window.TalkDemoAudio.resume();
      if (!window.TalkDemoAudio.ready()) throw new Error('Tap again to enable audio.');
      audio.hidden = true;
      emit({ ns: 'metahuman-talk', event_name: 'audio-enabled' });
    } catch (error) { audio.textContent = error.message; }
  });
})(window);
