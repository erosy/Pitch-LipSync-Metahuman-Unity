mergeInto(LibraryManager.library, {
  TalkDemoPublish: function (json) {
    if (window.TalkDemoTransport) window.TalkDemoTransport.publish(UTF8ToString(json));
  },
  TalkDemoAudioInit: function () {
    window.TalkDemoAudio = {
      resume: function () { return WEBAudio.audioContext.resume(); },
      ready: function () { return WEBAudio.audioContext.state === 'running'; }
    };
  },
  TalkDemoAudioReady: function () {
    return typeof WEBAudio !== 'undefined' && WEBAudio.audioContext && WEBAudio.audioContext.state === 'running' ? 1 : 0;
  }
});
