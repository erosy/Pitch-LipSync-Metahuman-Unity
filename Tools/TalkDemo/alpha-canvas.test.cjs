'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../Assets/WebGLTemplates/TalkDemo/alpha-canvas.js'), 'utf8');
const canvas = {};
class Context {
  constructor(canvas) { this.canvas = canvas; }
  configure(descriptor) { this.descriptor = descriptor; return 7; }
}
vm.runInNewContext(source, { document: { getElementById: () => canvas }, GPUCanvasContext: Context });
const target = new Context(canvas);
const unrelated = new Context({});
const descriptor = { alphaMode: 'opaque', device: {}, format: 'bgra8unorm' };
assert.equal(target.configure(descriptor), 7);
assert.equal(target.descriptor.alphaMode, 'premultiplied');
assert.equal(target.descriptor.device, descriptor.device);
assert.equal(target.descriptor.format, descriptor.format);
assert.equal(descriptor.alphaMode, 'opaque');
target.configure(descriptor); // resize/recovery configures the same context again
assert.equal(target.descriptor.alphaMode, 'premultiplied');
unrelated.configure(descriptor);
assert.equal(unrelated.descriptor, descriptor);
assert.doesNotThrow(() => vm.runInNewContext(source, { document: { getElementById: () => canvas } }));
console.log('Alpha canvas: target, resize, unrelated contexts and WebGL-only environment passed.');
// WebGL opaque-alpha fixup must not suppress normal rendering clears.
const calls = [];
const gl = {
  COLOR_WRITEMASK: 1, COLOR_CLEAR_VALUE: 2, DRAW_FRAMEBUFFER_BINDING: 3,
  FRAMEBUFFER_BINDING: 4, FRAMEBUFFER: 5, DRAW_FRAMEBUFFER: 6, READ_FRAMEBUFFER: 7,
  COLOR_BUFFER_BIT: 16, DEPTH_BUFFER_BIT: 32,
  getParameter(key) { return key === 1 ? [true,true,true,true] : key === 2 ? [0,0,0,0] : null; },
  colorMask() {}, clearColor() {}, bindFramebuffer() {}, clear(mask) { calls.push(mask); }
};
const glCanvas = { getContext: () => gl };
vm.runInNewContext(source, { document: { getElementById: () => glCanvas } });
const context = glCanvas.getContext('webgl2');
const wrappedClear = context.clear;
assert.equal(glCanvas.getContext('webgl2').clear, wrappedClear);
context.clearColor(0,0,0,1); context.colorMask(false,false,false,true);
context.clear(16); assert.equal(calls.length,0);
context.clear(48); assert.equal(calls.length,1); // combined color/depth
context.colorMask(true,true,true,true); context.clear(16); assert.equal(calls.length,2);
context.colorMask(false,false,false,true); context.clearColor(0,0,0,0); context.clear(16); assert.equal(calls.length,3);
context.clearColor(0,0,0,1); context.bindFramebuffer(5,{}); context.clear(16); assert.equal(calls.length,4);
context.bindFramebuffer(6,null); context.bindFramebuffer(7,{}); context.clear(16); assert.equal(calls.length,4); // read binding does not change draw binding
assert.equal(context,gl);
console.log('WebGL alpha fixup: repeated context, regular clears, depth, transparent clears and offscreen rendering passed.');

