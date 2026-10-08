import{d as m,s as c,W as I,X as b}from"./index-CK55GjKL.js";import{d as y}from"./fileUtils-B-Rww-_3.js";import{g as f,e as d,k as g,b as p,E as u,l as w}from"./einvoiceHtmlPreviewToolbar-CQAFqlv0.js";const h=`${b}/EInvoice`;function l(t){return t.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function X(t,e){return`<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <title>${l(e)}</title>
  <style>
    body { margin: 0; padding: 16px; background: #f8fafc; color: #0f172a; }
    pre { margin: 0; white-space: pre-wrap; word-break: break-word; font: 13px/1.5 Consolas, "Courier New", monospace; }
  </style>
</head>
<body><pre>${l(t)}</pre></body>
</html>`}function v(t,e){const r=t.trim();if(!r)return!1;const o=e.trim()||p(u.XML_PREVIEW_TITLE,"XML Preview"),a=X(r,o),i=new Blob([a],{type:"text/html;charset=utf-8"}),n=URL.createObjectURL(i);return window.open(n,"_blank")?(window.setTimeout(()=>{URL.revokeObjectURL(n)},6e5),!0):(URL.revokeObjectURL(n),!1)}async function T({xml:t,title:e,emptyMessage:r=w(),unableToOpenMessage:o=f(),notifyUnableToOpen:a}){const i=n=>{a?a(n):c(n,"error",4e3)};try{const n=t.trim();return n?v(n,(e==null?void 0:e.trim())||p(u.XML_PREVIEW_TITLE,"XML Preview"))?!0:(i(o),!1):(i(r),!1)}catch(n){const s=n instanceof Error?n.message:o;return i(s),!1}}async function E(t){const e=Number(t);if(!Number.isFinite(e)||e<=0)throw new Error("INVOICE_ID is required");if(!m().trim())throw new Error(d());const r=await I.get(`${h}/${e}/xml`,{responseType:"text"}),o=typeof r.data=="string"?r.data:String(r.data??"");if(!o.trim())throw new Error(w());return o}async function _(t,e){const r=await E(t),a=`EInvoice_${Number(t)}.xml`;y(new Blob([r],{type:"application/xml;charset=utf-8"}),a)}async function x(t,e){const r=Number(t);if(!Number.isFinite(r)||r<=0)return!1;if(!m().trim())return c(d(),"error",4e3),!1;const o=await E(r),a=(e==null?void 0:e.trim())||g(r);return v(o,a)}async function R({invoiceId:t,title:e,notifyUnableToOpen:r}){const o=f(),a=i=>{r?r(i):c(i,"error",4e3)};try{return await x(t,e)?!0:(a(o),!1)}catch(i){const n=i instanceof Error?i.message:o;return a(n),!1}}export{T as a,_ as d,R as o};
