mergeInto(LibraryManager.library, {
  EthanHairComparisonPublish: function (json) {
    window.dispatchEvent(new CustomEvent('ethan-hair-sample', { detail: JSON.parse(UTF8ToString(json)) }));
  }
});
