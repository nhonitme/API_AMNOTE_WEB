import{r as e,bk as c,bl as o}from"./index-Fufvmd3n.js";import{N as l}from"./button-x-3OB483.js";/*!
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
 */const N=e.memo(e.forwardRef((t,r)=>{const n=e.useRef(null);e.useImperativeHandle(r,()=>({instance(){var s;return(s=n.current)==null?void 0:s.getInstance()}}),[]);const a=e.useMemo(()=>["value"],[]),p=e.useMemo(()=>["onChange","onContentReady","onCopy","onCut","onDisposing","onEnterKey","onFocusIn","onFocusOut","onInitialized","onInput","onKeyDown","onKeyUp","onPaste","onValueChanged"],[]),m=e.useMemo(()=>({defaultValue:"value"}),[]),i=e.useMemo(()=>({button:{optionName:"buttons",isCollectionItem:!0},format:{optionName:"format",isCollectionItem:!1}}),[]);return e.createElement(c,{WidgetClass:l,ref:n,subscribableOptions:a,independentEvents:p,defaults:m,expectedChildren:i,...t})})),u=t=>e.createElement(o,{...t,elementDescriptor:{OptionName:"buttons",IsCollectionItem:!0,ExpectedChildren:{options:{optionName:"options",isCollectionItem:!1}}}});Object.assign(u,{componentType:"option"});const d=t=>e.createElement(o,{...t,elementDescriptor:{OptionName:"format"}});Object.assign(d,{componentType:"option"});const b=t=>e.createElement(o,{...t,elementDescriptor:{OptionName:"options",TemplateProps:[{tmplOption:"template",render:"render",component:"component"}]}});Object.assign(b,{componentType:"option"});export{N};
