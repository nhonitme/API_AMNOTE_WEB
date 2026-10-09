import{d as m,s as c,ae as y,W as I,X as b}from"./index-Fufvmd3n.js";import{g as f,e as u,k as g,b as d,E as p,l as w}from"./einvoiceHtmlPreviewToolbar-CBfej-e7.js";const h=`${b}/EInvoice`;function l(r){return r.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function X(r,n){return`<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <title>${l(n)}</title>
  <style>
    body { margin: 0; padding: 16px; background: #f8fafc; color: #0f172a; }
    pre { margin: 0; white-space: pre-wrap; word-break: break-word; font: 13px/1.5 Consolas, "Courier New", monospace; }
  </style>
</head>
<body><pre>${l(r)}</pre></body>
</html>`}function v(r,n){const e=r.trim();if(!e)return!1;const o=n.trim()||d(p.XML_PREVIEW_TITLE,"XML Preview"),a=X(e,o),i=new Blob([a],{type:"text/html;charset=utf-8"}),t=URL.createObjectURL(i);return window.open(t,"_blank")?(window.setTimeout(()=>{URL.revokeObjectURL(t)},6e5),!0):(URL.revokeObjectURL(t),!1)}async function P({xml:r,title:n,emptyMessage:e=w(),unableToOpenMessage:o=f(),notifyUnableToOpen:a}){const i=t=>{a?a(t):c(t,"error",4e3)};try{const t=r.trim();return t?v(t,(n==null?void 0:n.trim())||d(p.XML_PREVIEW_TITLE,"XML Preview"))?!0:(i(o),!1):(i(e),!1)}catch(t){const s=t instanceof Error?t.message:o;return i(s),!1}}async function E(r,n){const e=Number(r);if(!Number.isFinite(e)||e<=0)throw new Error("INVOICE_ID is required");if(!m().trim())throw new Error(u());const o=await I.get(`${h}/${e}/xml`,{responseType:"text",signal:n}),a=typeof o.data=="string"?o.data:String(o.data??"");if(!a.trim())throw new Error(w());return a}async function T(r,n,e){const a=`EInvoice_${Number(r)}.xml`;return y({fileName:a,batchId:e,load:async i=>{const t=await E(r,i);return new Blob([t],{type:"application/xml;charset=utf-8"})}})}async function N(r,n){const e=Number(r);if(!Number.isFinite(e)||e<=0)return!1;if(!m().trim())return c(u(),"error",4e3),!1;const o=await E(e),a=(n==null?void 0:n.trim())||g(e);return v(o,a)}async function _({invoiceId:r,title:n,notifyUnableToOpen:e}){const o=f(),a=i=>{e?e(i):c(i,"error",4e3)};try{return await N(r,n)?!0:(a(o),!1)}catch(i){const t=i instanceof Error?i.message:o;return a(t),!1}}export{P as a,T as d,_ as o};
