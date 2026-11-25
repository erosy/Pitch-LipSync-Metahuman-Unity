(function () {
  'use strict';

  const canvas = document.getElementById('unity-canvas');
  const hair = document.getElementById('hair');
  const ratio = document.getElementById('pixel-ratio');
  const background = document.getElementById('background');
  const sample = document.getElementById('sample');
  const download = document.getElementById('download');
  const status = document.getElementById('sample-status');
  const results = document.getElementById('results');
  const query = new URLSearchParams(location.search);
  const cap = query.get('cap') === '1.5' ? 1.5 : 2;
  if (['Cards', 'Ribbons', 'Hidden'].includes(query.get('hair'))) hair.value = query.get('hair');
  ratio.value = String(cap);
  let unity, run;

  function bufferSize() {
    return [canvas.width, canvas.height];
  }

  function lock(locked) {
    hair.disabled = ratio.disabled = background.disabled = sample.disabled = locked;
  }

  window.EthanHairTest = {
    cap,
    attach(instance) {
      unity = instance;
      lock(false);
      unity.SendMessage('Talk Demo', 'SetHairMode', hair.value);
    }
  };

  hair.addEventListener('change', () => unity.SendMessage('Talk Demo', 'SetHairMode', hair.value));
  ratio.addEventListener('change', () => {
    query.set('cap', ratio.value);
    query.set('hair', hair.value);
    location.search = query.toString();
  });
  background.addEventListener('change', () =>
    document.getElementById('stage').classList.toggle('light', background.checked));

  sample.addEventListener('click', () => {
    lock(true);
    download.disabled = true;
    results.textContent = '';
    run = {
      capturedAt: new Date().toISOString(),
      browser: navigator.userAgent,
      canvasCssPixels: [canvas.clientWidth, canvas.clientHeight],
      devicePixelRatio: window.devicePixelRatio,
      cap, initialDrawingBuffer: bufferSize(),
      background: background.checked ? 'light' : 'dark',
      invalidated: document.hidden, samples: []
    };
    unity.SendMessage('Talk Demo', 'StartSampleRun');
  });

  document.addEventListener('visibilitychange', () => {
    if (run && sample.disabled && document.hidden) run.invalidated = true;
  });
  new MutationObserver(() => {
    if (run && sample.disabled && String(bufferSize()) !== String(run.initialDrawingBuffer))
      run.invalidated = true;
  }).observe(canvas, { attributes: true, attributeFilter: ['width', 'height'] });

  window.addEventListener('ethan-hair-sample', ({ detail }) => {
    if (!run) return;
    status.textContent = detail.status;
    if (detail.frames > 0 && detail.sample > run.samples.length) {
      const drawingBuffer = bufferSize();
      if (String(drawingBuffer) !== String(run.initialDrawingBuffer)) run.invalidated = true;
      run.samples.push({ ...detail, drawingBuffer });
      const { sample: number, mode, api, medianMs, p95Ms } = detail;
      results.textContent += `#${number} ${mode}, ${api}, ${drawingBuffer.join('×')}\n`
        + `Median ${medianMs.toFixed(2)} ms · p95 ${p95Ms.toFixed(2)} ms\n`;
    }
    if (detail.status === 'Complete' || detail.status.startsWith('Wait') || detail.status.startsWith('Run interrupted')) {
      lock(false);
      if (detail.status === 'Complete') {
        download.disabled = false;
        if (run.invalidated) status.textContent = 'Run affected by a hidden tab or resized buffer. Repeat it.';
      }
    }
  });

  download.addEventListener('click', () => {
    const url = URL.createObjectURL(new Blob([JSON.stringify(run, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `ethan-hair-${run.samples[0].api}-${run.samples[0].mode}-${cap}.json`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  });
})();
