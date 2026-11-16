(function () {
  function hide(el) {
    el.style.setProperty("display", "none", "important");
    el.style.setProperty("height", "0px", "important");
    el.style.setProperty("max-height", "0px", "important");
    el.style.setProperty("overflow", "hidden", "important");
    el.style.setProperty("opacity", "0", "important");
    el.style.setProperty("visibility", "hidden", "important");
    el.style.setProperty("pointer-events", "none", "important");
  }

  function defineSilent(name) {
    if (customElements.get(name)) {
      return;
    }

    customElements.define(
      name,
      class extends HTMLElement {
        connectedCallback() {
          hide(this);
        }
      },
    );
  }

  defineSilent("dx-license");
  defineSilent("dx-license-trigger");

  var closed = new WeakSet();

  function closePanel(panel) {
    if (!(panel instanceof HTMLElement) || closed.has(panel)) {
      return;
    }

    closed.add(panel);
    var icon = panel.querySelector("svg");
    var button = icon && icon.parentElement;
    if (button) {
      button.click();
      return;
    }

    hide(panel);
  }

  function sweep() {
    document.querySelectorAll("dx-license").forEach(closePanel);
  }

  function arm() {
    sweep();
    if (!document.body) {
      return;
    }

    new MutationObserver(sweep).observe(document.body, { childList: true });
  }

  if (document.body) {
    arm();
  } else {
    document.addEventListener("DOMContentLoaded", arm);
  }
})();
