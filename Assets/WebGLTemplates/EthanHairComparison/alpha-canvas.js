'use strict';
// Unity 6000.5 browser-compositing workarounds, scoped to this iframe's canvas.
(() => {
  const canvas = document.getElementById('unity-canvas');
  if (!canvas) return;
  if (typeof GPUCanvasContext !== 'undefined') {
    const original = GPUCanvasContext.prototype.configure;
    GPUCanvasContext.prototype.configure = function (descriptor) {
      return original.call(this, this.canvas === canvas
        ? { ...descriptor, alphaMode: 'premultiplied' }
        : descriptor);
    };
  }

  const originalGetContext = canvas.getContext;
  if (typeof originalGetContext !== 'function') return;
  const installed = new WeakSet();
  canvas.getContext = function (type, ...args) {
    const gl = originalGetContext.call(this, type, ...args);
    if (!gl || !['webgl', 'webgl2', 'experimental-webgl'].includes(type) || installed.has(gl)) return gl;
    installed.add(gl);
    // Track state instead of querying the GPU on every clear (important on phones).
    let writeMask = Array.from(gl.getParameter(gl.COLOR_WRITEMASK));
    let clearAlpha = gl.getParameter(gl.COLOR_CLEAR_VALUE)[3];
    let drawFramebuffer = gl.getParameter(gl.DRAW_FRAMEBUFFER_BINDING ?? gl.FRAMEBUFFER_BINDING);
    const colorMask = gl.colorMask;
    const clearColor = gl.clearColor;
    const bindFramebuffer = gl.bindFramebuffer;
    const clear = gl.clear;
    gl.colorMask = function (r, g, b, a) {
      const result = colorMask.call(this, r, g, b, a);
      writeMask = [!!r, !!g, !!b, !!a];
      return result;
    };
    gl.clearColor = function (r, g, b, a) {
      const result = clearColor.call(this, r, g, b, a);
      clearAlpha = Math.min(1, Math.max(0, a));
      return result;
    };
    gl.bindFramebuffer = function (target, framebuffer) {
      const result = bindFramebuffer.call(this, target, framebuffer);
      if (target === gl.FRAMEBUFFER || target === gl.DRAW_FRAMEBUFFER) drawFramebuffer = framebuffer;
      return result;
    };
    gl.clear = function (mask) {
      // Unity's end-of-frame opaque-alpha fixup. Keep the alpha already rendered
      // by URP; do not skip ordinary clears, depth/stencil, or offscreen work.
      if (mask === gl.COLOR_BUFFER_BIT && drawFramebuffer === null && clearAlpha === 1
          && !writeMask[0] && !writeMask[1] && !writeMask[2] && writeMask[3]) return;
      return clear.call(this, mask);
    };
    return gl;
  };
})();
