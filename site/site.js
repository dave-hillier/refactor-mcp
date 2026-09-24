(function () {
  var root = document.documentElement;
  var toggle = document.querySelector('.theme-toggle');
  if (toggle) toggle.addEventListener('click', function () {
    var dark = root.dataset.theme ? root.dataset.theme === 'dark'
      : window.matchMedia('(prefers-color-scheme: dark)').matches;
    root.dataset.theme = dark ? 'light' : 'dark';
    try { localStorage.setItem('theme', root.dataset.theme); } catch (e) {}
  });

  // Index: filter refactorings by text and tier.
  var input = document.getElementById('filter');
  if (input) {
    var tier = 'all';
    var apply = function () {
      var q = input.value.trim().toLowerCase().split(/\s+/).filter(Boolean);
      var any = false;
      document.querySelectorAll('.tier').forEach(function (sec) {
        var shown = 0;
        var tierOk = tier === 'all' || sec.dataset.tier === tier;
        sec.querySelectorAll('.card').forEach(function (card) {
          var text = card.dataset.search;
          var ok = tierOk && q.every(function (w) { return text.indexOf(w) >= 0; });
          card.hidden = !ok;
          if (ok) shown++;
        });
        sec.hidden = shown === 0;
        if (shown) any = true;
      });
      document.getElementById('no-results').hidden = any;
    };
    input.addEventListener('input', apply);
    document.querySelectorAll('.filter .chip').forEach(function (chip) {
      chip.addEventListener('click', function () {
        document.querySelectorAll('.filter .chip').forEach(function (c) { c.classList.remove('active'); });
        chip.classList.add('active');
        tier = chip.dataset.tier;
        apply();
      });
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === '/' && document.activeElement !== input) { e.preventDefault(); input.focus(); }
    });
  }

  // Refactoring page: expand/collapse, filter by kind, open the linked case.
  document.querySelectorAll('[data-expand]').forEach(function (b) {
    b.addEventListener('click', function () {
      var open = b.dataset.expand === '1';
      document.querySelectorAll('.case > details').forEach(function (d) { d.open = open; });
    });
  });
  document.querySelectorAll('.toc .chip').forEach(function (chip) {
    chip.addEventListener('click', function () {
      document.querySelectorAll('.toc .chip').forEach(function (c) { c.classList.remove('active'); });
      chip.classList.add('active');
      var kind = chip.dataset.kind;
      document.querySelectorAll('.case').forEach(function (c) {
        c.hidden = kind !== 'all' && c.dataset.kind !== kind;
      });
      document.querySelectorAll('.toc li').forEach(function (li) {
        li.hidden = kind !== 'all' && !li.classList.contains(kind === 'error' ? 'err' : 'ok');
      });
    });
  });
  var openHash = function () {
    var id = decodeURIComponent(location.hash.slice(1));
    var el = id && document.getElementById(id);
    if (el && el.classList.contains('case')) {
      el.querySelector('details').open = true;
      el.scrollIntoView();
    }
  };
  window.addEventListener('hashchange', openHash);
  openHash();
})();
