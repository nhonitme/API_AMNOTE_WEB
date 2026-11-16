import{r as e,bs as c,fc as l}from"./index-YogiXTMr.js";/*!
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
 */const d=e.memo(e.forwardRef((t,o)=>{const n=e.useRef(null);e.useImperativeHandle(o,()=>({instance(){var s;return(s=n.current)==null?void 0:s.getInstance()}}),[]);const a=e.useMemo(()=>["value"],[]),r=e.useMemo(()=>["onContentReady","onDisposing","onInitialized","onValueChanged"],[]),u=e.useMemo(()=>({defaultValue:"value"}),[]);return e.createElement(c,{WidgetClass:l,ref:n,subscribableOptions:a,independentEvents:r,defaults:u,...t})}));export{d as C};
