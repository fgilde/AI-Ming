---
title: Contact & Support
layout: default
nav_order: 8
permalink: /contact/
description: Get in touch about PowerAim, or help keep the project going.
---

# Contact &amp; support
{: .fs-9 }

Questions, bug reports and ideas go straight to the developer. And if PowerAim is worth
something to you, there is a way to say so.
{: .fs-6 .fw-300 }

---

<script type="module">import('https://connect.gilde.org/widgets/v1.js?load=' + Date.now());</script>

## Get in touch

For anything reproducible — a crash, a detection that misbehaves, a game that needs a profile —
an [issue on GitHub](https://github.com/fgilde/AI-Ming/issues) is the better place: it is public,
searchable, and other users benefit from the answer. Everything else fits here.

<gilde-contact project="fgilde/AI-Ming" widget="contact" data-btn="primary" theme="dark" accent="#8e5ae0" language="auto" title="Contact PowerAim" width="560" radius="18" padding="28" show-logo="true" show-description="false" show-homepage="false" show-preview-notice="false" show-footer="false">Contact PowerAim</gilde-contact>

## Support the project

PowerAim is free, source-available and has no key system, no paywall and no ads — and it stays
that way. Supporting it is entirely optional; it pays for the things the project cannot do for
free, such as a code signing certificate.

<gilde-support project="fgilde/AI-Ming" widget="support" data-btn="primary" theme="dark" accent="#60cdff" language="auto" title="Support PowerAim" width="560" radius="18" padding="28" show-logo="true" show-description="true" show-homepage="false" show-preview-notice="false" show-footer="false" show-support-hint="false" support-layout="rows" show-support-icons="true" show-support-qr="true">Support PowerAim</gilde-support>

<script type="module">
(async function () {
  var base =
    "button.primary{display:inline-flex;align-items:center;gap:9px;font-family:inherit;" +
    "font-weight:600;font-size:15px;padding:11px 20px;border-radius:11px;cursor:pointer;" +
    "border:1px solid transparent;white-space:nowrap;" +
    "transition:transform .15s ease,box-shadow .25s ease,background .2s,border-color .2s}" +
    "button.primary:active{transform:translateY(1px)}";
  var styles = {
    primary: base +
      "button.primary{background:linear-gradient(110deg,#8e5ae0 0%,#60cdff 100%);color:#0a0614;" +
      "box-shadow:0 6px 30px -8px rgba(142,90,224,.6)}" +
      "button.primary:hover{box-shadow:0 10px 38px -8px rgba(96,205,255,.6);transform:translateY(-2px)}",
    ghost: base +
      "button.primary{background:rgba(255,255,255,.04);border-color:rgba(255,255,255,.2);color:#e6e1e8}" +
      "button.primary:hover{background:rgba(255,255,255,.08);border-color:#8e5ae0;transform:translateY(-2px)}"
  };
  for (var tag of ['gilde-contact', 'gilde-support']) {
    await customElements.whenDefined(tag);
    document.querySelectorAll(tag).forEach(function (el) {
      var root = el.shadowRoot;
      if (!root || !('adoptedStyleSheets' in root)) return;   // styling is cosmetic, never fatal
      var sheet = new CSSStyleSheet();
      sheet.replaceSync(styles[el.dataset.btn === 'ghost' ? 'ghost' : 'primary']);
      root.adoptedStyleSheets = [].concat(root.adoptedStyleSheets, sheet);
    });
  }

  // The support dialog is min(560px, 100vw - 28px) wide, and its rows layout puts a QR code beside
  // every provider. Below roughly 480px of dialog width that squeezes each label to one character
  // per line, so the codes are dropped on narrow screens — where they are useless anyway, the
  // reader is already holding the device they would scan with.
  var wide = matchMedia('(min-width: 540px)');
  var applyQr = function () {
    document.querySelectorAll('gilde-support').forEach(function (el) {
      el.setAttribute('show-support-qr', wide.matches ? 'true' : 'false');
    });
  };
  wide.addEventListener('change', applyQr);
  applyQr();
})();
</script>
