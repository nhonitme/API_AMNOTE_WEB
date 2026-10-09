import{r as e,bk as c}from"./index-Fufvmd3n.js";import{T as i}from"./m_sortable-DKl8FH_Y.js";/*!
 * devextreme-react
 * Version: 25.2.5
 * Build date: Fri Feb 20 2026
 *
 * Copyright (c) 2012 - 2026 Developer Express Inc. ALL RIGHTS RESERVED
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file in the root of the project for details.
 *
 * https://github.com/DevExpress/DevExtreme
 */const p=e.memo(e.forwardRef((t,s)=>{const n=e.useRef(null);e.useImperativeHandle(s,()=>({instance(){var o;return(o=n.current)==null?void 0:o.getInstance()}}),[]);const a=e.useMemo(()=>["value"],[]),r=e.useMemo(()=>["onChange","onContentReady","onCopy","onCut","onDisposing","onEnterKey","onFocusIn","onFocusOut","onInitialized","onInput","onKeyDown","onKeyUp","onPaste","onValueChanged"],[]),u=e.useMemo(()=>({defaultValue:"value"}),[]);return e.createElement(c,{WidgetClass:i,ref:n,subscribableOptions:a,independentEvents:r,defaults:u,...t})}));export{p as T};
