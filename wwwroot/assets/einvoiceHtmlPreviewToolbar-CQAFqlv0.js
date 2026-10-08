import{hT as a,fS as s,hU as N}from"./index-CK55GjKL.js";const p="language-labels";function I(e,t){const i=a(p,s());return N(i,e)??t}function _(e,t){return t.reduce((i,o,r)=>i.replace(new RegExp(`\\{${r}\\}`,"g"),String(o)),e)}const E={LOAD_FAILED:"LOAD_FAILED",LOADING:"LOADING",CANCEL:"CANCEL",YES:"YES",NO:"NO",REQUIRED:"REQUIRED",SEARCH:"SEARCH",SELECTED:"SELECTED",SAVE_SUCCESS:"SAVE_SUCCESS",DELETE_SUCCESS:"DELETE_SUCCESS",TAX_CD:"TAX_CD",MTRACUU:"MTRACUU",PRINT:"PRINT",PRINT_PDF:"PRINT_PDF",PREVIEW_OPEN_FAILED:"PREVIEW_OPEN_FAILED",PREVIEW_OPENED_HINT:"PREVIEW_OPENED_HINT",PREVIEW_OPENED_CONVERTED_HINT:"PREVIEW_OPENED_CONVERTED_HINT",PREVIEW_SINGLE:"PREVIEW_SINGLE",XML_PREVIEW_SINGLE:"XML_PREVIEW_SINGLE",XML_PREVIEW_TITLE:"XML_PREVIEW_TITLE",XML_TITLE:"XML_TITLE",XML_EMPTY:"XML_EMPTY",PDF_GENERATING:"PDF_GENERATING",PDF_EXPORT_FAILED:"PDF_EXPORT_FAILED",PDF_OPENED_TAB:"PDF_OPENED_TAB",PDF_DOWNLOADED:"PDF_DOWNLOADED",BATCH_DOWNLOAD_STARTED:"BATCH_DOWNLOAD_STARTED",BATCH_DOWNLOAD_SUCCESS:"BATCH_DOWNLOAD_SUCCESS",BATCH_DOWNLOAD_XML_STARTED:"BATCH_DOWNLOAD_XML_STARTED",BATCH_DOWNLOAD_XML_SUCCESS:"BATCH_DOWNLOAD_XML_SUCCESS",BATCH_DOWNLOAD_ITEM_FAILED:"BATCH_DOWNLOAD_ITEM_FAILED",XML_EXPORT_FAILED:"XML_EXPORT_FAILED",COMPANY_NOT_SELECTED:"COMPANY_NOT_SELECTED",SIGNED_YES:"IS_SIGNED",SIGNED_NO:"SIGNED_NO",SIGN_XML:"SIGN_XML",SIGN_SEND_CQT:"SIGN_SEND_CQT",SIGN_FAILED:"SIGN_FAILED",SIGN_CONFIRM_BATCH:"SIGN_CONFIRM_BATCH",SIGN_SUCCESS_COUNT:"SIGN_SUCCESS_COUNT",SIGN_SKIP_COUNT:"SIGN_SKIP_COUNT",SIGN_SKIP_SIGNED:"SIGN_SKIP_SIGNED",SIGN_SKIP_EMPTY_XML:"SIGN_SKIP_EMPTY_XML",SIGN_PARTIAL_SUCCESS:"SIGN_PARTIAL_SUCCESS",SIGNED_DELETE_BLOCKED:"SIGNED_DELETE_BLOCKED",CERTIFICATE_SELECT:"CERTIFICATE_SELECT",PROVIDER_DUPLICATE:"PROVIDER_DUPLICATE",PROVIDER_NOT_FOUND:"DECL_PROVIDER_NOT_FOUND",PREVIEW_HINT:"PREVIEW_HINT",SIGN_RECORD_COUNT:"SIGN_RECORD_COUNT",NO_ROWS_SELECTED:"NO_ROWS_SELECTED",LOOKUP_TITLE:"LOOKUP_TITLE",LOOKUP_NOT_FOUND:"LOOKUP_NOT_FOUND",LOOKUP_EMPTY:"LOOKUP_EMPTY",NQ204_CHECKBOX:"EINV_NQ204_CHECKBOX",NQ204_TTKHAC_LABEL:"EINV_NQ204_TTKHAC_LABEL",COMMERCIAL_DISCOUNT_DETAIL_BLOCKS_HEADER:"EINV_COMMERCIAL_DISCOUNT_DETAIL_BLOCKS_HEADER"};function n(e,t){return I(e,t)}function L(){return n(E.PREVIEW_OPEN_FAILED,"Unable to open preview")}function O(){return n(E.PDF_EXPORT_FAILED,"Unable to export PDF")}function P(){return n(E.XML_EXPORT_FAILED,"Unable to export XML")}function d(){return n(E.XML_EMPTY,"XML is empty")}function C(){return n(E.COMPANY_NOT_SELECTED,"Company code is not selected")}function A(e){return _(n(E.XML_TITLE,"XML #{0}"),[e])}function c(e){const t=e??n(E.PRINT_PDF,"Print PDF");return _(n(E.PREVIEW_OPENED_HINT,"Preview opened. Click {0} on preview to export."),[t])}function R(e,t,i){return`${e(t,i)} ${e(E.REQUIRED,"is required")}`}const T="amnote-einvoice-export-pdf-done",D="amnote-einvoice-export-pdf-error";function S(){return{exportPdf:n(E.PRINT_PDF,"Print PDF"),print:n(E.PRINT,"Print"),exportingPdf:n(E.PDF_GENERATING,"Generating PDF..."),noOpener:n("PREVIEW_NO_OPENER","Cannot export PDF because the preview window is not linked to the application."),exportFailed:O()}}function f(e,t){const i=JSON.stringify(t.exportPayload),o=S(),r=`
<div id="amnote-preview-actions">
  <button type="button" id="amnote-export-pdf-btn" class="amnote-preview-action-btn" title=${JSON.stringify(o.exportPdf)} aria-label=${JSON.stringify(o.exportPdf)}>
    <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
      <polyline points="14 2 14 8 20 8"></polyline>
      <line x1="12" y1="18" x2="12" y2="12"></line>
      <polyline points="9 15 12 18 15 15"></polyline>
    </svg>
  </button>
  <button type="button" id="amnote-print-btn" class="amnote-preview-action-btn" title=${JSON.stringify(o.print)} aria-label=${JSON.stringify(o.print)}>
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
  var exportPayload = ${i};
  var exportBtn = document.getElementById("amnote-export-pdf-btn");
  var printBtn = document.getElementById("amnote-print-btn");
  var parentOrigin = ${JSON.stringify(window.location.origin)};
  var labels = ${JSON.stringify(o)};

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
        Object.assign({ type: ${JSON.stringify(t.exportMessageType)} }, exportPayload),
        parentOrigin,
      );
    });
  }

  window.addEventListener("message", function (event) {
    if (event.origin !== parentOrigin || !event.data) {
      return;
    }

    if (event.data.type === ${JSON.stringify(T)}) {
      resetExportButton();
      return;
    }

    if (event.data.type === ${JSON.stringify(D)}) {
      resetExportButton();
      window.alert(event.data.message || labels.exportFailed);
    }
  });
})();
<\/script>
`;return e.includes("</body>")?e.replace("</body>",`${r}</body>`):`${e}${r}`}export{E,T as P,_ as a,n as b,P as c,O as d,C as e,R as f,L as g,c as h,D as i,f as j,A as k,d as l};
