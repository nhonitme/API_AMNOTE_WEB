import{d as s,b as e,E as i}from"./einvoiceI18n-Cw01U1iY.js";const p="amnote-einvoice-export-pdf-done",l="amnote-einvoice-export-pdf-error";function d(){return{exportPdf:e(i.PRINT_PDF,"Print PDF"),print:e(i.PRINT,"Print"),exportingPdf:e(i.PDF_GENERATING,"Generating PDF..."),noOpener:e("PREVIEW_NO_OPENER","Cannot export PDF because the preview window is not linked to the application."),exportFailed:s()}}function x(n,o){const a=JSON.stringify(o.exportPayload),t=d(),r=`
<div id="amnote-preview-actions">
  <button type="button" id="amnote-export-pdf-btn" class="amnote-preview-action-btn" title=${JSON.stringify(t.exportPdf)} aria-label=${JSON.stringify(t.exportPdf)}>
    <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
      <polyline points="14 2 14 8 20 8"></polyline>
      <line x1="12" y1="18" x2="12" y2="12"></line>
      <polyline points="9 15 12 18 15 15"></polyline>
    </svg>
  </button>
  <button type="button" id="amnote-print-btn" class="amnote-preview-action-btn" title=${JSON.stringify(t.print)} aria-label=${JSON.stringify(t.print)}>
    <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2">
      <polyline points="6 9 6 2 18 2 18 9"></polyline>
      <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path>
      <rect x="6" y="14" width="12" height="8"></rect>
    </svg>
  </button>
</div>
<style>
  #amnote-preview-actions {
    position: fixed;
    right: 20px;
    bottom: 20px;
    z-index: 100000;
    display: flex;
    flex-direction: column;
    gap: 10px;
    font-family: "Segoe UI", Arial, sans-serif;
  }
  #amnote-preview-actions .amnote-preview-action-btn {
    width: 52px;
    height: 52px;
    border: none;
    border-radius: 50%;
    background: #0f766e;
    color: #fff;
    box-shadow: 0 4px 14px rgba(15, 118, 110, 0.45);
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: transform 0.15s ease, box-shadow 0.15s ease, background 0.15s ease;
  }
  #amnote-preview-actions .amnote-preview-action-btn:hover:not(:disabled) {
    transform: translateY(-1px);
    box-shadow: 0 6px 18px rgba(15, 118, 110, 0.5);
    background: #0d9488;
  }
  #amnote-preview-actions .amnote-preview-action-btn:disabled {
    opacity: 0.65;
    cursor: wait;
  }
  #amnote-preview-actions .amnote-preview-action-btn.is-loading svg {
    opacity: 0.35;
  }
  @media print {
    #amnote-preview-actions {
      display: none !important;
    }
  }
</style>
<script>
(function () {
  var exportPayload = ${a};
  var exportBtn = document.getElementById("amnote-export-pdf-btn");
  var printBtn = document.getElementById("amnote-print-btn");
  var parentOrigin = ${JSON.stringify(window.location.origin)};
  var labels = ${JSON.stringify(t)};

  function resetExportButton() {
    if (!exportBtn) return;
    exportBtn.disabled = false;
    exportBtn.classList.remove("is-loading");
    exportBtn.setAttribute("title", labels.exportPdf);
    exportBtn.setAttribute("aria-label", labels.exportPdf);
  }

  if (printBtn) {
    printBtn.addEventListener("click", function () {
      window.print();
    });
  }

  if (exportBtn) {
    exportBtn.addEventListener("click", function () {
      if (!window.opener) {
        window.alert(labels.noOpener);
        return;
      }

      exportBtn.disabled = true;
      exportBtn.classList.add("is-loading");
      exportBtn.setAttribute("title", labels.exportingPdf);
      exportBtn.setAttribute("aria-label", labels.exportingPdf);
      window.opener.postMessage(
        Object.assign({ type: ${JSON.stringify(o.exportMessageType)} }, exportPayload),
        parentOrigin,
      );
    });
  }

  window.addEventListener("message", function (event) {
    if (event.origin !== parentOrigin || !event.data) {
      return;
    }

    if (event.data.type === ${JSON.stringify(p)}) {
      resetExportButton();
      return;
    }

    if (event.data.type === ${JSON.stringify(l)}) {
      resetExportButton();
      window.alert(event.data.message || labels.exportFailed);
    }
  });
})();
<\/script>
`;return n.includes("</body>")?n.replace("</body>",`${r}</body>`):`${n}${r}`}export{p as P,l as a,x as i};
